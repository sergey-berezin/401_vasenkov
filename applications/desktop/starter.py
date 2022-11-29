import os
import psutil
import common


if __name__ == '__main__':
    r, w = os.pipe()
    server_pid = os.fork()
    if server_pid:
        os.close(w)
        r = os.fdopen(r)
        port = None
        while True:
            line = r.readline()
            if 'Now listening on:' in line:
                for elem in line.split():
                    if elem.startswith('http'):
                        port = int(elem.split(':')[-1])
                        break
                break
        print('port:', port)
        common.build_index_html(f'https://localhost:{port}/')
        os.system('npm start')
        parent = psutil.Process(server_pid)
        for child in parent.children(recursive=True):
            child.kill()
        parent.kill()
    else:
        os.close(r)
        os.dup2(w, 1)
        os.execlp('dotnet', 'dotnet', 'run', '--project', '../AspNetWebApplication')
