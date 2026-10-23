"""Rewrites the absolute symbolic links under a root file system as relative ones, so that they resolve inside it wherever it is."""

import os
import sys

root = os.path.abspath(sys.argv[1])

for directory, directories, files in os.walk(root):
    for name in directories + files:
        path = os.path.join(directory, name)
        if os.path.islink(path):
            target = os.readlink(path)
            if target.startswith("/"):
                os.remove(path)
                os.symlink(os.path.relpath(root + target, directory), path)
