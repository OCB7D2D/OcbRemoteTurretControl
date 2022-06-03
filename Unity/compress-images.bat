@echo off

webmerge -f "%~dp0\optimize.conf.xml" --optimize --jobs 12 -l 6 %*

pause