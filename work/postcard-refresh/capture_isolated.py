import hashlib, json, os, pathlib, re, subprocess, sys, time, signal, fcntl

repo = pathlib.Path(__file__).resolve().parents[2]
project = repo / 'work/postcard-refresh/UnityReview'
unity = '/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity'
# Keep one queue per project, and terminate the owned Unity child before exit.
# A cancelled queue must not orphan a process that locks the next review.
queue_lock = open(repo / 'work/full-visual-overhaul/.capture-queue.lock', 'a+')
try:
    fcntl.flock(queue_lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
except BlockingIOError:
    sys.exit('Another capture queue owns this project.')
current_child = None
def stop_queue(signum, frame):
    if current_child is not None and current_child.poll() is None:
        current_child.terminate()
        try: current_child.wait(timeout=10)
        except subprocess.TimeoutExpired:
            current_child.kill()
            current_child.wait()
    sys.exit(128 + signum)
signal.signal(signal.SIGTERM, stop_queue)
signal.signal(signal.SIGINT, stop_queue)
jobs = json.loads(pathlib.Path(sys.argv[1]).read_text())
results = []
for job in jobs:
    print('START ' + job['name'], flush=True)
    env = os.environ.copy()
    env.update(job.get('env', {}))
    files = [p for p in (project/'Assets').rglob('*') if p.is_file() and p.suffix in {'.cs','.shader','.hlsl'}]
    source = {str(p.relative_to(repo)):hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
    (repo/'work/full-visual-overhaul'/(job['name']+'.source.json')).write_text(json.dumps(source,indent=2))
    argv = [unity, '-projectPath', str(project), '-logFile', str(repo / 'work/full-visual-overhaul' / (job['name'] + '.log'))]
    if job.get('method'): argv.extend(['-executeMethod',job['method']])
    argv.extend(job.get('args',[]))
    if job.get('batch', True): argv.append('-batchmode')
    if job.get('quit', False): argv.append('-quit')
    start = time.time()
    current_child = subprocess.Popen(argv, env=env, stdout=subprocess.DEVNULL, stderr=subprocess.STDOUT)
    code = current_child.wait()
    current_child = None
    log = (repo/'work/full-visual-overhaul'/(job['name']+'.log')).read_text(errors='replace')
    shader_errors = sorted(set(line for line in log.splitlines() if 'Shader error in ' in line))
    compile_errors = sorted(set(line for line in log.splitlines() if re.search(r'error CS\d+',line)))
    if code == 0 and (shader_errors or compile_errors): code = 2
    row = {'name': job['name'], 'exit': code, 'seconds': round(time.time() - start, 2), 'shaderErrors':shader_errors, 'compileErrors':compile_errors}
    results.append(row)
    print('FINISH ' + json.dumps(row), flush=True)
    pathlib.Path(sys.argv[1] + '.results.json').write_text(json.dumps(results, indent=2))
    if code != 0 and not job.get('continue_on_failure', False): sys.exit(code)
