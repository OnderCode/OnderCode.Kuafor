@echo off
rem Emulatorde ekran goruntuleri. Sonuc: logs\shots-*.log, PNG'ler: docs\raw-*\
cd /d "%~dp0"
set SDK=%LOCALAPPDATA%\Android\Sdk
set ADB="%SDK%\platform-tools\adb.exe"
if not exist logs mkdir logs
set S=logs\shots-%RANDOM%.log
set O=docs\raw-%RANDOM%
echo basladi > %S%
%ADB% start-server >> %S% 2>&1
%ADB% devices | findstr /c:"emulator-" > nul
if errorlevel 1 (
  echo emulator aciliyor >> %S%
  start "" "%SDK%\emulator\emulator.exe" -avd Pixel_7 -no-snapshot-save -no-boot-anim -gpu auto
)
%ADB% wait-for-device >> %S% 2>&1
:boot
%ADB% shell getprop sys.boot_completed 2>nul | findstr 1 > nul
if errorlevel 1 ( timeout /t 3 /nobreak > nul & goto boot )
echo acildi >> %S%
timeout /t 5 /nobreak > nul
%ADB% install -r dist\OnderCode.Kuafor-1.0.0.apk >> %S% 2>&1
rem Durum cubugu: 11:20, tam sarj, tam sinyal, bildirimsiz
%ADB% shell settings put global sysui_demo_allowed 1
%ADB% shell am broadcast -a com.android.systemui.demo -e command enter > nul
%ADB% shell am broadcast -a com.android.systemui.demo -e command clock -e hhmm 1120 > nul
%ADB% shell am broadcast -a com.android.systemui.demo -e command battery -e level 100 -e plugged false > nul
%ADB% shell am broadcast -a com.android.systemui.demo -e command network -e wifi show -e level 4 > nul
%ADB% shell am broadcast -a com.android.systemui.demo -e command network -e mobile show -e datatype none -e level 4 > nul
%ADB% shell am broadcast -a com.android.systemui.demo -e command notifications -e visible false > nul
%ADB% shell cmd uimode night no >> %S% 2>&1
%ADB% shell am force-stop com.ondercode.kuafor
%ADB% shell pm clear com.ondercode.kuafor >> %S% 2>&1
%ADB% shell am start -n com.ondercode.kuafor/com.ondercode.kuafor.MainActivity --ez shots true >> %S% 2>&1
set /a N=0
:wait
timeout /t 5 /nobreak > nul
set /a N+=1
%ADB% shell ls /sdcard/Android/data/com.ondercode.kuafor/files/shots/ 2>nul | findstr /c:"done.txt" /c:"error.txt" > nul
if errorlevel 1 ( if %N% LSS 60 goto wait )
mkdir %O%
%ADB% pull /sdcard/Android/data/com.ondercode.kuafor/files/shots/. %O% >> %S% 2>&1
%ADB% logcat -d -t 400 *:E > %O%\logcat.txt 2>&1
dir /b %O% >> %S%
echo bitti >> %S%
