'use strict';

const {
  evaluateAuditReport,
  hasGateFailures,
} = require('../scripts/npm-audit-gate');

const exception = {
  advisory: 'GHSA-mh99-v99m-4gvg',
  source: 1124334,
  package: 'brace-expansion',
  severity: 'high',
  rationale: 'Latest supported test tooling has not adopted the patched major version.',
  scope: 'Development-only test tooling.',
  expires: '2026-08-31',
};

function report(vulnerabilities) {
  return {
    auditReportVersion: 2,
    vulnerabilities,
  };
}

function advisory(
  source = 1124334,
  id = 'GHSA-mh99-v99m-4gvg',
  severity = 'high') {
  return {
    source,
    severity,
    url: `https://github.com/advisories/${id}`,
  };
}

describe('npm audit gate', () => {
  test('allows only the exact advisory and its transitive vulnerability chain', () => {
    const result = evaluateAuditReport(report({
      'brace-expansion': {
        severity: 'high',
        via: [advisory()],
      },
      minimatch: {
        severity: 'high',
        via: ['brace-expansion'],
      },
      glob: {
        severity: 'high',
        via: ['minimatch'],
      },
    }), [exception], new Date('2026-07-25T00:00:00Z'));

    expect(result.failures).toEqual([]);
    expect(result.allowed.map((finding) => finding.name)).toEqual([
      'brace-expansion',
      'minimatch',
      'glob',
    ]);
    expect(result.usedExceptions).toEqual([exception]);
  });

  test('ignores non-blocking entries alongside the exact High advisory', () => {
    const result = evaluateAuditReport(report({
      'brace-expansion': {
        severity: 'high',
        via: [
          advisory(),
          advisory(9999999, 'GHSA-xxxx-yyyy-zzzz', 'moderate'),
          'moderate-helper',
        ],
      },
      'moderate-helper': {
        severity: 'moderate',
        via: [advisory(9999998, 'GHSA-aaaa-bbbb-cccc', 'moderate')],
      },
    }), [exception], new Date('2026-07-25T00:00:00Z'));

    expect(result.failures).toEqual([]);
    expect(result.allowed).toEqual([
      { name: 'brace-expansion', severity: 'high' },
    ]);
  });

  test('rejects an unrelated High advisory and packages that depend on it', () => {
    const result = evaluateAuditReport(report({
      'brace-expansion': {
        severity: 'high',
        via: [advisory()],
      },
      minimatch: {
        severity: 'high',
        via: ['brace-expansion'],
      },
      'unsafe-package': {
        severity: 'high',
        via: [advisory(9999999, 'GHSA-xxxx-yyyy-zzzz')],
      },
      consumer: {
        severity: 'critical',
        via: ['unsafe-package'],
      },
    }), [exception], new Date('2026-07-25T00:00:00Z'));

    expect(result.failures).toEqual([
      { name: 'unsafe-package', severity: 'high' },
      { name: 'consumer', severity: 'critical' },
    ]);
  });

  test('rejects the allowed advisory after its expiry date', () => {
    const result = evaluateAuditReport(report({
      'brace-expansion': {
        severity: 'high',
        via: [advisory()],
      },
      minimatch: {
        severity: 'high',
        via: ['brace-expansion'],
      },
    }), [exception], new Date('2026-09-01T00:00:00Z'));

    expect(result.failures).toEqual([
      { name: 'brace-expansion', severity: 'high' },
      { name: 'minimatch', severity: 'high' },
    ]);
    expect(result.expiredExceptions).toEqual([exception]);
    expect(hasGateFailures(result)).toBe(true);
  });

  test('fails on an expired exception even after its advisory disappears', () => {
    const result = evaluateAuditReport(
      report({}),
      [exception],
      new Date('2026-09-01T00:00:00Z'));

    expect(result.failures).toEqual([]);
    expect(result.expiredExceptions).toEqual([exception]);
    expect(hasGateFailures(result)).toBe(true);
  });

  test('rejects invalid calendar dates in exception configuration', () => {
    expect(() => evaluateAuditReport(
      report({}),
      [{ ...exception, expires: '2026-99-99' }],
      new Date('2026-07-25T00:00:00Z')))
      .toThrow('must be a valid calendar date');
  });

  test('does not apply development exceptions to the production audit', () => {
    const productionResult = evaluateAuditReport(report({
      'brace-expansion': {
        severity: 'high',
        via: [advisory()],
      },
    }), [], new Date('2026-07-25T00:00:00Z'));

    expect(productionResult.failures).toEqual([
      { name: 'brace-expansion', severity: 'high' },
    ]);
    expect(hasGateFailures(productionResult)).toBe(true);
  });

  test('does not block unrelated findings below High severity', () => {
    const result = evaluateAuditReport(report({
      'informational-package': {
        severity: 'moderate',
        via: [advisory(9999999, 'GHSA-xxxx-yyyy-zzzz')],
      },
    }), [exception], new Date('2026-07-25T00:00:00Z'));

    expect(result.failures).toEqual([]);
    expect(result.allowed).toEqual([]);
  });
});
