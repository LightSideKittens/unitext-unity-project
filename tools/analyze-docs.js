const d = require('../Assets/api-roslyn.json');

// Типы без summary по категориям
console.log('=== TYPES WITHOUT SUMMARY (75) ===\n');
const noSummary = d.types.filter(t => !t.summary);
const byCategory = {};
noSummary.forEach(t => {
  const cat = t.category || 'other';
  if (!byCategory[cat]) byCategory[cat] = [];
  byCategory[cat].push(t.id);
});

Object.keys(byCategory).sort().forEach(cat => {
  console.log(`[${cat}] (${byCategory[cat].length}):`);
  byCategory[cat].forEach(id => console.log(`  - ${id}`));
  console.log('');
});

// Методы без summary в важных типах
console.log('\n=== METHODS WITHOUT SUMMARY IN CORE TYPES ===\n');
const coreTypes = d.types.filter(t => t.category === 'core' && t.summary);
coreTypes.forEach(type => {
  const methods = (type.members?.methods || []).filter(m => !m.summary && m.accessModifier === 'public');
  if (methods.length > 0) {
    console.log(`${type.id} (${methods.length} undocumented methods):`);
    methods.slice(0, 5).forEach(m => console.log(`  - ${m.name}`));
    if (methods.length > 5) console.log(`  ... and ${methods.length - 5} more`);
    console.log('');
  }
});

// Методы с параметрами но без param docs
console.log('\n=== METHODS WITH PARAMS BUT NO PARAM DOCS ===\n');
let count = 0;
d.types.filter(t => t.category === 'core').forEach(type => {
  const methods = (type.members?.methods || []).filter(m =>
    m.paramTypes && m.paramTypes.length > 0 &&
    (!m.params || m.params.length === 0) &&
    m.accessModifier === 'public'
  );
  if (methods.length > 0) {
    console.log(`${type.id}:`);
    methods.slice(0, 3).forEach(m => console.log(`  - ${m.name}(${m.paramTypes.join(', ')})`));
    if (methods.length > 3) console.log(`  ... and ${methods.length - 3} more`);
    count += methods.length;
    console.log('');
  }
});
console.log(`Total: ${count} methods need param docs`);
