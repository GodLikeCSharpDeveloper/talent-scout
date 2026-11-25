// Local contract stand-in. Synthetic payloads only; never forward requests to OpenAI.
// Response envelope follows https://github.com/openai/openai-dotnet structured-output examples.
const http = require('http');
const requests = [];
function send(res, code, body) {
  res.writeHead(code, { 'Content-Type': 'application/json' });
  res.end(JSON.stringify(body));
}
function readBody(req) {
  if (req.body !== undefined) return Promise.resolve(req.body);
  return new Promise((resolve, reject) => {
    let text = '';
    req.on('data', chunk => text += chunk);
    req.on('end', () => { try { resolve(JSON.parse(text)); } catch (error) { reject(error); } });
    req.on('error', reject);
  });
}
http.createServer(async (req, res) => {
  try {
    if (req.method === 'GET' && req.url === '/requests') return send(res, 200, requests);
    if (req.method !== 'POST' || req.url !== '/v1/chat/completions') return send(res, 501, { error: 'Path not modeled' });
    const input = await readBody(req);
    requests.push(input);
    if (!input.model || !Array.isArray(input.messages) || input.response_format?.type !== 'json_schema' || input.response_format?.json_schema?.strict !== true)
      return send(res, 400, { error: { message: 'Invalid SDK request', type: 'invalid_request_error' } });
    const model = input.model;
    if (model === 'timeout') await new Promise(resolve => setTimeout(resolve, 1500));
    if (/^error(401|429|500)$/.test(model))
      return send(res, Number(model.slice(5)), { error: { message: 'private upstream details', type: 'api_error', code: 'test_error' } });
    const name = input.response_format.json_schema.name;
    let content;
    if (name === 'profile_assessment') content = { meetsRequiredCriteria: true, score: 0.95, reason: 'Relevant C# experience' };
    else if (name === 'introduction') content = { text: 'Hello, are you interested in this role?' };
    else if (name === 'reply_assessment') content = { interest: 'Interested', confidence: 0.99, reason: 'Explicit interest' };
    else return send(res, 400, { error: { message: 'Unknown response schema', type: 'invalid_request_error' } });
    if (model === 'missing') content = { reason: 'Missing required fields' };
    if (model === 'invalid-enum') content = { interest: 'InventedStatus', confidence: 0.99, reason: 'Invalid enum' };
    send(res, 200, {
      id: 'chatcmpl-test', object: 'chat.completion', created: 1730000000, model,
      choices: [{ index: 0, message: { role: 'assistant', content: model === 'malformed' ? 'not-json' : JSON.stringify(content), refusal: model === 'refused' ? 'Cannot comply' : null }, finish_reason: model === 'truncated' ? 'length' : 'stop' }],
      usage: { prompt_tokens: 10, completion_tokens: 10, total_tokens: 20 }
    });
  } catch { send(res, 500, { error: { message: 'Mock failed', type: 'api_error' } }); }
}).listen(process.env.PORT || 15963);
