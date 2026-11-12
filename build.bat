@echo off
set DOTNET_CLI_UI_LANGUAGE=en
rem Testleri calistirir ve Android APK uretir. Sonuc: logs\build-*.log, APK: dist\
cd /d "%~dp0"
call test.bat
if not exist logs mkdir logs
set B=logs\build-%RANDOM%
set ANDROID_HOME=%LOCALAPPDATA%\Android\Sdk
set JAVA_HOME=C:\Program Files\Android\openjdk\jdk-21.0.8
dotnet publish src\Kuafor.App\Kuafor.App.csproj -f net10.0-android -c Release -p:AndroidSdkDirectory="%ANDROID_HOME%" -p:JavaSdkDirectory="%JAVA_HOME%" > %B%-full.log 2>&1
set RC=%errorlevel%
echo CIKIS KODU %RC% > %B%.log
findstr /r /c:"error [A-Z][A-Z]*[0-9]" %B%-full.log >> %B%.log
if not "%RC%"=="0" exit /b 1
if not exist dist mkdir dist
copy /y src\Kuafor.App\bin\Release\net10.0-android\publish\com.ondercode.kuafor-Signed.apk dist\OnderCode.Kuafor-1.0.0.apk >> %B%.log
for %%F in (dist\OnderCode.Kuafor-1.0.0.apk) do echo APK %%~zF bayt >> %B%.log
exit /b 0
