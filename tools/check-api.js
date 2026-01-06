const old = require('./api.json');
const newApi = require('../Assets/api-roslyn.json');

console.log('=== OLD api.json method keys ===');
const oldMethod = old.types[0]?.members?.methods?.[0];
console.log(Object.keys(oldMethod || {}));

console.log('');
console.log('=== NEW api-roslyn.json method keys ===');
const newMethod = newApi.types[0]?.members?.methods?.[0];
console.log(Object.keys(newMethod || {}));

console.log('');
console.log('=== Missing in new ===');
const oldKeys = new Set(Object.keys(oldMethod || {}));
const newKeys = new Set(Object.keys(newMethod || {}));
const missing = [...oldKeys].filter(k => !newKeys.has(k));
console.log(missing);

console.log('');
console.log('=== New in new ===');
const added = [...newKeys].filter(k => !oldKeys.has(k));
console.log(added);

console.log('');
console.log('=== Check for constructors ===');
let ctorCount = 0;
newApi.types.forEach(t => {
  const ctors = (t.members?.methods || []).filter(m => m.isConstructor);
  if (ctors.length > 0) {
    ctorCount += ctors.length;
  }
});
console.log('Total constructors found:', ctorCount);

console.log('');
console.log('=== Check for seeAlso in methods ===');
let seeAlsoCount = 0;
newApi.types.forEach(t => {
  (t.members?.methods || []).forEach(m => {
    if (m.seeAlso && m.seeAlso.length > 0) seeAlsoCount++;
  });
});
console.log('Methods with seeAlso:', seeAlsoCount);
