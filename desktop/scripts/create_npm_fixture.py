"""Create isolated npm installations for native manager integration checks."""
import json
import os
from pathlib import Path
import shutil
import sys

root = Path(sys.argv[1]).resolve()
node = Path(sys.argv[2]).resolve()
for index in (1, 2):
    prefix = root / f"npm {index} & space"
    bin_dir = prefix if os.name == "nt" else prefix / "bin"
    modules = prefix / ("node_modules" if os.name == "nt" else "lib/node_modules")
    package = modules / "@konbakuyomu/smart-search"
    bin_dir.mkdir(parents=True, exist_ok=True)
    package.mkdir(parents=True, exist_ok=True)
    manifest = {"name": "@konbakuyomu/smart-search", "version": "1.2.3", "smartSearchBinary": index == 2}
    (package / "package.json").write_text(json.dumps(manifest))
    wrapper = package / "npm/bin/smart-search.js"
    wrapper.parent.mkdir(parents=True)
    wrapper.write_text("const p=require('../../package.json'); if(!p.smartSearchBinary) { require('fs').writeFileSync(" + json.dumps(str(root / "legacy-executed")) + ", 'bad'); process.exit(1); } console.log(JSON.stringify({product:'smart-search',version:p.version,desktop_protocol_version:1}));")
    script = modules / "npm/bin/npm-cli.js"
    script.parent.mkdir(parents=True)
    script.write_text("#!/usr/bin/env node\n" + f"const prefix={json.dumps(str(prefix))}, modules={json.dumps(str(modules))};\n" + """
const fs=require('fs'),path=require('path'),args=process.argv.slice(2);
if(args[0]==='--version') console.log('10.9.1');
else if(args[0]==='prefix') console.log(prefix);
else if(args[0]==='root') console.log(modules);
else if(args[0]==='view') console.log(JSON.stringify({version:'1.2.4',smartSearchBinary:true}));
else if(args[0]==='install' || args[0]==='uninstall') {
  if(args[args.indexOf('--prefix')+1] !== prefix) process.exit(7);
  fs.writeFileSync(path.join(prefix,'operations.json'),JSON.stringify(args));
  const package=path.join(modules,'@konbakuyomu/smart-search');
  if(args[0]==='uninstall') fs.rmSync(package,{recursive:true});
  else { const file=path.join(package,'package.json'), data=JSON.parse(fs.readFileSync(file)); data.version='1.2.4';fs.writeFileSync(file,JSON.stringify(data)); }
} else process.exit(9);
""")
    script.chmod(0o755)
    if os.name == "nt":
        shutil.copy2(node, bin_dir / "node.exe")
        (bin_dir / "npm.cmd").write_text('@echo SHOULD NOT RUN CMD\r\nexit /b 99\r\n')
    else:
        (bin_dir / "node").symlink_to(node)
        (bin_dir / "npm").symlink_to(script)
print(root)
