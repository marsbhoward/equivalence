const fs = require('fs'), zlib = require('zlib');
process.argv[2] = '/dev/null';
const { sheetFrames } = (() => { const m = require('./mock8.js'); return m; })();
