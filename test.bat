@echo off
set DOTNET_CLI_UI_LANGUAGE=en
rem Cekirdek testleri. Sonuc: logs\test-*.log
cd /d "%~dp0"
if not exist logs mkdir logs
set T=logs\test-%RANDOM%
dotnet test tests\Kuafor.Tests\Kuafor.Tests.csproj -c Release > %T%-full.log 2>&1
echo CIKIS KODU %errorlevel% > %T%.log
findstr /c:"error " /c:"Passed!" /c:"Failed!" /c:"[FAIL]" /c:"Assert" /c:"Exception" /c:"at Kuafor" %T%-full.log >> %T%.log
