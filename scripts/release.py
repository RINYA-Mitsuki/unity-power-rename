"""Publish a verified draft release; retries never overwrite existing asset bytes."""
import json, os, subprocess, tempfile
from pathlib import Path
from package import build

def gh(*args, **kwargs):
    return subprocess.run(['gh',*args],check=True,capture_output=True,**kwargs).stdout

def main():
    repo=os.environ['GITHUB_REPOSITORY']
    root=next(Path('Packages').glob('*/package.json')).parent
    m,archive,digest=build(root,'dist',repo)
    tag='v'+m['version']
    ref=os.environ.get('GITHUB_REF','')
    if ref not in ('refs/heads/main','refs/tags/'+tag): raise ValueError('Release only from main or matching version tag')
    head=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip()
    tags=json.loads(gh('api',f'repos/{repo}/git/matching-refs/tags/{tag}'))
    exact=[t for t in tags if t['ref']=='refs/tags/'+tag]
    if exact:
        subprocess.run(['git','fetch','origin','tag',tag],check=True)
        tagged=subprocess.check_output(['git','rev-parse',tag+'^{commit}'],text=True).strip()
        if tagged!=head: raise ValueError('Existing version tag points at a different commit; bump version')
    releases=json.loads(gh('api','--paginate','--slurp',f'repos/{repo}/releases?per_page=100'))
    release=next((r for page in releases for r in page if r['tag_name']==tag),None)
    if release is None:
        gh('release','create',tag,'--repo',repo,'--target',head,'--draft','--title',f"Unity Power Rename {m['version']}",
           '--notes-file',str(root/'CHANGELOG.md'))
        release=json.loads(gh('api',f'repos/{repo}/releases/tags/{tag}'))
    assets={a['name']:a for a in release['assets']}
    for path in (archive,Path('dist/package.json'),Path('dist/SHA256SUMS')):
        if path.name in assets:
            data=gh('api',f"repos/{repo}/releases/assets/{assets[path.name]['id']}",'-H','Accept: application/octet-stream')
            if data!=path.read_bytes(): raise ValueError('Existing asset differs: '+path.name)
        elif release['draft']:
            gh('release','upload',tag,str(path),'--repo',repo)
        else:
            raise ValueError('Published release missing asset; refusing mutation')
    if release['draft']:
        gh('release','edit',tag,'--repo',repo,'--draft=false','--prerelease='+str('-' in m['version']).lower())
    print(f'Published/verified {repo} {tag}; SHA256={digest}')

if __name__=='__main__': main()
