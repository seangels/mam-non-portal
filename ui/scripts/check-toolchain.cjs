const expectedNode = '14.21.3';
const expectedNpm = '8.19.4';
const actualNode = process.versions.node;
const actualNpm = process.env.npm_config_user_agent?.match(/npm\/([^ ]+)/)?.[1] ?? '';

if (actualNode !== expectedNode || actualNpm !== expectedNpm) {
  console.error(`UI yêu cầu Node ${expectedNode} và npm ${expectedNpm}; hiện tại là Node ${actualNode}, npm ${actualNpm || 'không xác định'}.`);
  console.error(`Windows: chay '. .\\ui\\use-node-portable.ps1' tu workspace root truoc (xem ui/README.md muc Node portable).`);
  process.exit(1);
}
