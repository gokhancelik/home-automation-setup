# Factory I/O "Sorting by Weight" Scene Configuration for CloudHMI
# This script configures and runs simulation for the Sorting by Weight scene

Write-Host "Factory I/O - Sorting by Weight Scene Setup" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green

Write-Host "`nSorting by Weight Scene Configuration:" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

Write-Host "`nFactory I/O Configuration Steps:" -ForegroundColor Yellow
Write-Host "1. Open Factory I/O" -ForegroundColor White
Write-Host "2. Go to File -> Drivers -> Modbus TCP/IP Slave" -ForegroundColor White
Write-Host "3. Configure connection:" -ForegroundColor White
Write-Host "   - IP Address: 127.0.0.1" -ForegroundColor Gray
Write-Host "   - Port: 502" -ForegroundColor Gray
Write-Host "   - Update Rate: 100ms" -ForegroundColor Gray
Write-Host "4. Load the 'Sorting by Weight' scene" -ForegroundColor White
Write-Host "5. Map I/O points as follows:" -ForegroundColor White

Write-Host "`nI/O Mapping for Sorting by Weight:" -ForegroundColor Yellow
Write-Host "====================================" -ForegroundColor Yellow

Write-Host "`nINPUTS (Sensors):" -ForegroundColor Cyan
Write-Host "  %I0.0 (Input 1)  - Entry conveyor sensor" -ForegroundColor Gray
Write-Host "  %I0.1 (Input 2)  - Weight scale sensor" -ForegroundColor Gray
Write-Host "  %I0.2 (Input 3)  - Light part exit sensor" -ForegroundColor Gray
Write-Host "  %I0.3 (Input 4)  - Heavy part exit sensor" -ForegroundColor Gray
Write-Host "  %I0.4 (Input 5)  - Reject bin sensor" -ForegroundColor Gray

Write-Host "`nOUTPUTS (Actuators):" -ForegroundColor Cyan
Write-Host "  %Q0.0 (Coil 1)   - Entry conveyor motor" -ForegroundColor Gray
Write-Host "  %Q0.1 (Coil 2)   - Scale conveyor motor" -ForegroundColor Gray
Write-Host "  %Q0.2 (Coil 3)   - Light parts conveyor" -ForegroundColor Gray
Write-Host "  %Q0.3 (Coil 4)   - Heavy parts conveyor" -ForegroundColor Gray
Write-Host "  %Q0.4 (Coil 5)   - Diverter actuator" -ForegroundColor Gray

Write-Host "`nREGISTERS (Analog Values):" -ForegroundColor Cyan
Write-Host "  %MW0 (Reg 0-1)   - Current weight reading (grams)" -ForegroundColor Gray
Write-Host "  %MW2 (Reg 2-3)   - Weight threshold (grams)" -ForegroundColor Gray
Write-Host "  %MW4 (Reg 4-5)   - Light parts counter" -ForegroundColor Gray
Write-Host "  %MW6 (Reg 6-7)   - Heavy parts counter" -ForegroundColor Gray
Write-Host "  %MW8 (Reg 8-9)   - Total parts counter" -ForegroundColor Gray
Write-Host "  %MW10 (Reg 10-11) - Cycle time (milliseconds)" -ForegroundColor Gray

Write-Host "`nQuick Start Instructions:" -ForegroundColor Yellow
Write-Host "===========================" -ForegroundColor Yellow
Write-Host "1. Configure Factory I/O as shown above" -ForegroundColor White
Write-Host "2. Start the scene (press Play button)" -ForegroundColor White
Write-Host "3. Run our bridge script:" -ForegroundColor White
Write-Host "   .\scripts\factory-io-sorting-bridge.ps1" -ForegroundColor Gray
Write-Host "4. Monitor data in CloudHMI dashboard at:" -ForegroundColor White
Write-Host "   http://localhost:57822" -ForegroundColor Cyan

Write-Host "`nExpected Data Flow:" -ForegroundColor Yellow
Write-Host "=====================" -ForegroundColor Yellow
Write-Host "Factory I/O (Modbus) -> Python Bridge -> K8s MQTT -> CloudHMI Gateway -> InfluxDB -> Dashboard" -ForegroundColor Gray

Write-Host "`nReady to start? Run the bridge script next!" -ForegroundColor Green
