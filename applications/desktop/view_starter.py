import os
import argparse
import common


if __name__ == '__main__':
    parser = argparse.ArgumentParser(
        prog = 'View starter',
        description = 'Starts desktop application for remote server')
    parser.add_argument('-s', '--server', required=True, help='Server url (example: http://localhost:8888')
    args = parser.parse_args()
    common.build_index_html(args.server)
    os.system('npm start')
