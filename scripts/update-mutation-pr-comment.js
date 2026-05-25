const fs = require('fs');

function buildMutationSummaryCommentBody(marker, summaryMarkdown) {
  return `${marker}\n${summaryMarkdown}`;
}

function selectExistingMutationComment(comments, marker, botLogin = 'github-actions[bot]') {
  const matches = comments.filter((comment) =>
    comment &&
    comment.user &&
    comment.user.login === botLogin &&
    typeof comment.body === 'string' &&
    comment.body.includes(marker));
  return matches.length === 0 ? undefined : matches[matches.length - 1];
}

async function upsertMutationSummaryComment({ github, context, summaryPath, marker = '<!-- folderdiff-mutation-summary -->', botLogin = 'github-actions[bot]' }) {
  const body = buildMutationSummaryCommentBody(marker, fs.readFileSync(summaryPath, 'utf8'));
  const issue_number = context.issue.number;
  const { owner, repo } = context.repo;
  const comments = await github.paginate(github.rest.issues.listComments, {
    owner,
    repo,
    issue_number,
    per_page: 100,
  });
  const existing = selectExistingMutationComment(comments, marker, botLogin);

  if (existing) {
    await github.rest.issues.updateComment({
      owner,
      repo,
      comment_id: existing.id,
      body,
    });
    return { action: 'updated', commentId: existing.id };
  }

  const created = await github.rest.issues.createComment({
    owner,
    repo,
    issue_number,
    body,
  });
  return { action: 'created', commentId: created.data.id };
}

module.exports = {
  buildMutationSummaryCommentBody,
  selectExistingMutationComment,
  upsertMutationSummaryComment,
};
