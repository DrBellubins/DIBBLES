#!/bin/bash
find . -path "./obj/*" -prune -o -iname "*" -print > cs_file_list.txt

