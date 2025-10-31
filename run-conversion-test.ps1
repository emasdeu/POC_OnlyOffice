#!/usr/bin/env pwsh
<#
.SYNOPSIS
    OnlyOffice document conversion healthcheck test
.DESCRIPTION
    Performs a sample execution of test_pdf_source.pdf → test_pdf_source.docx conversion
    Manages port-forwards, runs conversion test, and validates output
.PARAMETER InputFile
    Input document file (default: test_files/test_pdf_source.pdf)
.PARAMETER OutputFormat
    Output format (default: docx)
#>
param(
    [string]$InputFile = "test_files/test_pdf_source.pdf",
    [string]$OutputFormat = "docx"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Configuration
$NAMESPACE = "onlyoffice"
$ONLYOFFICE_SERVICE = "onlyoffice-onlyoffice-documentserver"
$FILESERVER_SERVICE = "onlyoffice-onlyoffice-documentserver-fileserver"
$ONLYOFFICE_PORT = 8080
$FILESERVER_PORT = 9000
$PROJECT_ROOT = "c:\WK_SourceCode\POC_OnlyOffice"
$CONSOLE_APP = "OnlyOfficeConsoleApp"

Write-Host "=== OnlyOffice Conversion Test ===" -ForegroundColor Cyan
Write-Host "Input file: $InputFile" -ForegroundColor Gray
Write-Host "Output format: $OutputFormat" -ForegroundColor Gray
Write-Host ""

# Function to kill port-forward processes
function Stop-PortForwards {
    Write-Host "Stopping old port-forward processes..." -ForegroundColor Yellow
    $processes = Get-Process kubectl -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -match "port-forward" }
    if ($processes) {
        $processes | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 1
        Write-Host "[OK] Port-forward processes stopped" -ForegroundColor Green
    } else {
        Write-Host "[OK] No port-forward processes running" -ForegroundColor Green
    }
}

# Function to start port-forwards
function Start-PortForwards {
    Write-Host "Starting port-forwards..." -ForegroundColor Yellow
    
    # Start OnlyOffice port-forward
    Write-Host "  Starting OnlyOffice port-forward on ${ONLYOFFICE_PORT}..." -ForegroundColor Gray
    Start-Process -NoNewWindow -FilePath "kubectl" -ArgumentList "port-forward", "-n", $NAMESPACE, "svc/${ONLYOFFICE_SERVICE}", "${ONLYOFFICE_PORT}:80" | Out-Null
    
    # Start file server port-forward
    Write-Host "  Starting file server port-forward on ${FILESERVER_PORT}..." -ForegroundColor Gray
    Start-Process -NoNewWindow -FilePath "kubectl" -ArgumentList "port-forward", "-n", $NAMESPACE, "svc/${FILESERVER_SERVICE}", "${FILESERVER_PORT}:${FILESERVER_PORT}" | Out-Null
    
    # Wait for port-forwards to be ready
    Write-Host "  Waiting for port-forwards to be ready..." -ForegroundColor Gray
    $maxAttempts = 10
    $attempts = 0
    $ready = $false
    
    while ($attempts -lt $maxAttempts -and -not $ready) {
        Start-Sleep -Seconds 1
        $attempts++
        
        # Check if ports are listening
        $ports = netstat -ano 2>$null | Select-String ":${ONLYOFFICE_PORT}|:${FILESERVER_PORT}"
        if ($ports.Count -ge 4) {
            $ready = $true
        }
    }
    
    if ($ready) {
        Write-Host "[OK] Port-forwards ready" -ForegroundColor Green
        return $true
    } else {
        Write-Host "[FAIL] Port-forwards failed to start" -ForegroundColor Red
        return $false
    }
}

# Function to verify port connectivity
function Test-PortConnectivity {
    Write-Host "Testing port connectivity..." -ForegroundColor Yellow
    
    $allHealthy = $true
    
    # Test OnlyOffice
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:${ONLYOFFICE_PORT}/healthcheck" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            Write-Host "[OK] OnlyOffice port ${ONLYOFFICE_PORT}: Healthy" -ForegroundColor Green
        } else {
            Write-Host "[FAIL] OnlyOffice port ${ONLYOFFICE_PORT}: Status $($response.StatusCode)" -ForegroundColor Red
            $allHealthy = $false
        }
    } catch {
        Write-Host "[FAIL] OnlyOffice port ${ONLYOFFICE_PORT}: Connection error" -ForegroundColor Red
        $allHealthy = $false
    }
    
    # Test File Server
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:${FILESERVER_PORT}/health" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            Write-Host "[OK] File server port ${FILESERVER_PORT}: Healthy" -ForegroundColor Green
        } else {
            Write-Host "[FAIL] File server port ${FILESERVER_PORT}: Status $($response.StatusCode)" -ForegroundColor Red
            $allHealthy = $false
        }
    } catch {
        Write-Host "[FAIL] File server port ${FILESERVER_PORT}: Connection error" -ForegroundColor Red
        $allHealthy = $false
    }
    
    return $allHealthy
}

# Function to run conversion test
function Invoke-ConversionTest {
    Write-Host "Running conversion test..." -ForegroundColor Yellow
    
    $inputPath = Join-Path $PROJECT_ROOT $InputFile
    
    if (-not (Test-Path $inputPath)) {
        Write-Host "[FAIL] Input file not found: $inputPath" -ForegroundColor Red
        return $false
    }
    
    Push-Location $PROJECT_ROOT
    try {
        $output = & dotnet run --project $CONSOLE_APP $InputFile 2>&1
        $exitCode = $LASTEXITCODE
        
        # Print output
        $output | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
        
        if ($exitCode -eq 0) {
            Write-Host "[OK] Conversion test completed" -ForegroundColor Green
            return $true
        } else {
            Write-Host "[FAIL] Conversion test failed with exit code $exitCode" -ForegroundColor Red
            return $false
        }
    } finally {
        Pop-Location
    }
}

# Function to verify output file
function Verify-OutputFile {
    Write-Host "Checking for output file..." -ForegroundColor Yellow
    
    $outputFile = Join-Path $PROJECT_ROOT "test_files/test_pdf_source.${OutputFormat}"
    
    if (Test-Path $outputFile) {
        $fileSize = (Get-Item $outputFile).Length
        if ($fileSize -gt 0) {
            Write-Host "[OK] Output file created: $outputFile (${fileSize} bytes)" -ForegroundColor Green
            return $true
        } else {
            Write-Host "[FAIL] Output file is empty: $outputFile" -ForegroundColor Red
            return $false
        }
    } else {
        Write-Host "[FAIL] Output file not found: $outputFile" -ForegroundColor Red
        Write-Host "  Checking storage in pod..." -ForegroundColor Gray
        
        # Get pod name
        $pod = & kubectl get pods -n $NAMESPACE --sort-by=.metadata.creationTimestamp -o name 2>$null | Select-Object -First 1
        if ($pod) {
            $podName = $pod -replace "pod/", ""
            Write-Host "  Checking pod: $podName" -ForegroundColor Gray
            
            $files = & kubectl exec -n $NAMESPACE $podName -c file-server -- ls /var/lib/onlyoffice-storage 2>$null
            Write-Host "  Files in pod storage:" -ForegroundColor Gray
            $files | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
        }
        
        return $false
    }
}

# Main execution
try {
    # Step 1: Stop old port-forwards
    Stop-PortForwards
    Write-Host ""
    
    # Step 2: Start new port-forwards
    if (-not (Start-PortForwards)) {
        Write-Host ""
        Write-Host "FAILED: Could not start port-forwards" -ForegroundColor Red
        exit 1
    }
    Write-Host ""
    
    # Step 3: Test connectivity
    if (-not (Test-PortConnectivity)) {
        Write-Host ""
        Write-Host "WARNING: Some ports not responding" -ForegroundColor Yellow
        Write-Host ""
    } else {
        Write-Host ""
    }
    
    # Step 4: Run conversion test
    $testPassed = Invoke-ConversionTest
    Write-Host ""
    
    # Step 5: Check output
    if ($testPassed) {
        $outputFound = Verify-OutputFile
        Write-Host ""
        
        if ($outputFound) {
            Write-Host "=== SUCCESS ===" -ForegroundColor Green
            Write-Host "Document conversion completed successfully!" -ForegroundColor Green
            exit 0
        } else {
            Write-Host "=== PARTIAL SUCCESS ===" -ForegroundColor Yellow
            Write-Host "Conversion may have completed inside the cluster" -ForegroundColor Yellow
            exit 1
        }
    } else {
        Write-Host "=== FAILED ===" -ForegroundColor Red
        Write-Host "Conversion test failed" -ForegroundColor Red
        exit 1
    }
}
catch {
    Write-Host ""
    Write-Host "ERROR: $($_)" -ForegroundColor Red
    exit 1
}
