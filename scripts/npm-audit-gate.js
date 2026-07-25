'use strict';

const fs = require('fs');
const path = require('path');
const { spawnSync } = require('child_process');

const BLOCKING_SEVERITIES = new Set(['high', 'critical']);

function validateException(exception) {
  const requiredStrings = [
    'advisory',
    'package',
    'severity',
    'rationale',
    'scope',
    'expires',
  ];

  for (const field of requiredStrings) {
    if (typeof exception[field] !== 'string' || exception[field].trim() === '') {
      throw new Error(`npm audit exception field "${field}" must be a non-empty string.`);
    }
  }

  if (!Number.isInteger(exception.source)) {
    throw new Error('npm audit exception field "source" must be an integer.');
  }

  if (!/^\d{4}-\d{2}-\d{2}$/.test(exception.expires)) {
    throw new Error('npm audit exception field "expires" must use YYYY-MM-DD.');
  }

  const parsedExpiry = new Date(`${exception.expires}T00:00:00Z`);
  if (Number.isNaN(parsedExpiry.getTime())
      || parsedExpiry.toISOString().slice(0, 10) !== exception.expires) {
    throw new Error('npm audit exception field "expires" must be a valid calendar date.');
  }
}

function matchingException(vulnerabilityName, advisory, activeExceptions) {
  if (typeof advisory !== 'object' || advisory === null) {
    return undefined;
  }

  return activeExceptions.find((exception) =>
    vulnerabilityName === exception.package
    && advisory.source === exception.source
    && advisory.severity === exception.severity
    && advisory.url === `https://github.com/advisories/${exception.advisory}`);
}

function evaluateAuditReport(report, exceptions, now = new Date()) {
  if (!report || report.auditReportVersion !== 2
      || typeof report.vulnerabilities !== 'object'
      || report.vulnerabilities === null) {
    throw new Error('npm audit did not return a supported version 2 audit report.');
  }

  const currentDate = now.toISOString().slice(0, 10);
  for (const exception of exceptions) {
    validateException(exception);
  }

  const activeExceptions = exceptions.filter(
    (exception) => currentDate <= exception.expires);
  const expiredExceptions = exceptions.filter(
    (exception) => currentDate > exception.expires);
  const blockingVulnerabilities = Object.entries(report.vulnerabilities)
    .filter(([, vulnerability]) => BLOCKING_SEVERITIES.has(vulnerability.severity));
  const allowedNames = new Set();
  const usedExceptionIds = new Set();

  // Start from exact advisory matches, then allow only dependency chains made
  // entirely from those matches.
  // advisory の完全一致を起点とし、その一致だけで構成される依存チェーンに限って許可します。
  let changed = true;
  while (changed) {
    changed = false;
    for (const [name, vulnerability] of blockingVulnerabilities) {
      if (allowedNames.has(name) || !Array.isArray(vulnerability.via)
          || vulnerability.via.length === 0) {
        continue;
      }

      const blockingVia = vulnerability.via.filter((via) => {
        if (typeof via === 'string') {
          const referencedVulnerability = report.vulnerabilities[via];
          return !referencedVulnerability
            || BLOCKING_SEVERITIES.has(referencedVulnerability.severity);
        }

        return typeof via !== 'object' || via === null
          || typeof via.severity !== 'string'
          || BLOCKING_SEVERITIES.has(via.severity);
      });
      const matchedExceptions = [];
      const isAllowed = blockingVia.length > 0 && blockingVia.every((via) => {
        if (typeof via === 'string') {
          return allowedNames.has(via);
        }

        const exception = matchingException(name, via, activeExceptions);
        if (exception) {
          matchedExceptions.push(exception);
          return true;
        }

        return false;
      });

      if (isAllowed) {
        allowedNames.add(name);
        for (const exception of matchedExceptions) {
          usedExceptionIds.add(exception.advisory);
        }
        changed = true;
      }
    }
  }

  return {
    allowed: blockingVulnerabilities
      .filter(([name]) => allowedNames.has(name))
      .map(([name, vulnerability]) => ({ name, severity: vulnerability.severity })),
    failures: blockingVulnerabilities
      .filter(([name]) => !allowedNames.has(name))
      .map(([name, vulnerability]) => ({ name, severity: vulnerability.severity })),
    expiredExceptions,
    usedExceptions: activeExceptions.filter(
      (exception) => usedExceptionIds.has(exception.advisory)),
  };
}

function hasGateFailures(result) {
  return result.failures.length > 0 || result.expiredExceptions.length > 0;
}

function runAudit(repositoryRoot, arguments_) {
  const npmCommand = process.platform === 'win32' ? 'npm.cmd' : 'npm';
  const audit = spawnSync(npmCommand, ['audit', ...arguments_, '--json'], {
    cwd: repositoryRoot,
    encoding: 'utf8',
  });

  if (audit.error) {
    throw audit.error;
  }

  let report;
  try {
    report = JSON.parse(audit.stdout);
  } catch (error) {
    throw new Error(`npm audit returned invalid JSON: ${error.message}`);
  }

  if (!report || report.auditReportVersion !== 2) {
    throw new Error('npm audit did not return a supported version 2 audit report.');
  }

  return report;
}

function main() {
  const repositoryRoot = path.resolve(__dirname, '..');
  const configPath = path.join(repositoryRoot, 'npm-audit-exceptions.json');
  const config = JSON.parse(fs.readFileSync(configPath, 'utf8'));
  if (!Array.isArray(config.exceptions)) {
    throw new Error('npm-audit-exceptions.json must contain an exceptions array.');
  }

  const result = evaluateAuditReport(
    runAudit(repositoryRoot, []),
    config.exceptions);
  const productionResult = evaluateAuditReport(
    runAudit(repositoryRoot, ['--omit=dev']),
    []);
  for (const exception of result.usedExceptions) {
    console.warn(
      `Temporary npm audit exception: ${exception.advisory} `
      + `(scope: ${exception.scope}; expires: ${exception.expires})`);
  }

  if (result.expiredExceptions.length > 0) {
    console.error('Expired npm audit exceptions:');
    for (const exception of result.expiredExceptions) {
      console.error(`  - ${exception.advisory}: expired ${exception.expires}`);
    }
  }

  if (result.failures.length > 0 || productionResult.failures.length > 0) {
    console.error('Blocking npm audit findings:');
    for (const failure of result.failures) {
      console.error(`  - ${failure.name}: ${failure.severity} (all dependencies)`);
    }
    for (const failure of productionResult.failures) {
      console.error(`  - ${failure.name}: ${failure.severity} (production dependency)`);
    }
  }

  if (hasGateFailures(result) || hasGateFailures(productionResult)) {
    process.exitCode = 1;
    return;
  }

  console.log(
    `npm audit gate passed (${result.allowed.length} finding(s) covered by `
    + `${result.usedExceptions.length} active exception(s)).`);
}

if (require.main === module) {
  try {
    main();
  } catch (error) {
    console.error(`npm audit gate failed: ${error.message}`);
    process.exitCode = 1;
  }
}

module.exports = {
  evaluateAuditReport,
  hasGateFailures,
};
