@echo off
curl.exe --fail-with-body --silent --show-error ^
  "http://localhost:11434/api/embed" ^
  -H "Content-Type: application/json" ^
  -d "{\"model\":\"nomic-embed-text\",\"input\":\"The sky is blue because of Rayleigh scattering\"}"
exit /b %ERRORLEVEL%
