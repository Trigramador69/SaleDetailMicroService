# =============================================================================
# Script para verificar configuración de RabbitMQ - SaleDetail
# =============================================================================
# Requisitos: RabbitMQ corriendo en localhost:5672
# Panel web: http://localhost:15672 (guest/guest)
# =============================================================================

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "🐰 VERIFICACIÓN RABBITMQ - SaleDetail" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# 1. Verificar si RabbitMQ está corriendo
Write-Host "1️⃣  Verificando servicio RabbitMQ..." -ForegroundColor Yellow
$rabbitService = Get-Service -Name "RabbitMQ" -ErrorAction SilentlyContinue

if ($rabbitService) {
    if ($rabbitService.Status -eq "Running") {
        Write-Host "   ✅ RabbitMQ está corriendo" -ForegroundColor Green
    } else {
        Write-Host "   ❌ RabbitMQ está instalado pero NO corriendo" -ForegroundColor Red
        Write-Host "   Ejecuta: net start RabbitMQ" -ForegroundColor Yellow
        exit 1
    }
} else {
    Write-Host "   ⚠️  Servicio RabbitMQ no encontrado (puede estar corriendo de otra forma)" -ForegroundColor Yellow
}

# 2. Verificar conectividad al puerto
Write-Host "`n2️⃣  Verificando puerto 5672..." -ForegroundColor Yellow
$tcpTest = Test-NetConnection -ComputerName localhost -Port 5672 -WarningAction SilentlyContinue

if ($tcpTest.TcpTestSucceeded) {
    Write-Host "   ✅ Puerto 5672 accesible" -ForegroundColor Green
} else {
    Write-Host "   ❌ Puerto 5672 no accesible" -ForegroundColor Red
    Write-Host "   Verifica que RabbitMQ esté corriendo" -ForegroundColor Yellow
    exit 1
}

# 3. Obtener información de colas usando RabbitMQ HTTP API
Write-Host "`n3️⃣  Consultando colas via HTTP API (puerto 15672)..." -ForegroundColor Yellow

try {
    $creds = "guest:guest"
    $encodedCreds = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($creds))
    $headers = @{Authorization = "Basic $encodedCreds"}
    
    # Obtener exchanges
    Write-Host "`n   📦 EXCHANGES:" -ForegroundColor Cyan
    $exchanges = Invoke-RestMethod -Uri "http://localhost:15672/api/exchanges" -Headers $headers -Method Get
    $sagaExchange = $exchanges | Where-Object { $_.name -eq "saga.exchange" }
    
    if ($sagaExchange) {
        Write-Host "   ✅ saga.exchange encontrado (type: $($sagaExchange.type), durable: $($sagaExchange.durable))" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  saga.exchange NO encontrado - se creará al iniciar la app" -ForegroundColor Yellow
    }
    
    # Obtener colas
    Write-Host "`n   📬 COLAS:" -ForegroundColor Cyan
    $queues = Invoke-RestMethod -Uri "http://localhost:15672/api/queues" -Headers $headers -Method Get
    $saleDetailQueue = $queues | Where-Object { $_.name -eq "saledetail.queue" }
    
    if ($saleDetailQueue) {
        Write-Host "   ✅ saledetail.queue encontrada" -ForegroundColor Green
        Write-Host "      - Mensajes: $($saleDetailQueue.messages)" -ForegroundColor Gray
        Write-Host "      - Consumers: $($saleDetailQueue.consumers)" -ForegroundColor Gray
        Write-Host "      - Durable: $($saleDetailQueue.durable)" -ForegroundColor Gray
    } else {
        Write-Host "   ⚠️  saledetail.queue NO encontrada - se creará al iniciar la app" -ForegroundColor Yellow
    }
    
    # Obtener bindings
    if ($saleDetailQueue) {
        Write-Host "`n   🔗 BINDINGS para saledetail.queue:" -ForegroundColor Cyan
        $bindings = Invoke-RestMethod -Uri "http://localhost:15672/api/queues/%2F/saledetail.queue/bindings" -Headers $headers -Method Get
        
        $relevantBindings = $bindings | Where-Object { $_.source -eq "saga.exchange" }
        if ($relevantBindings) {
            foreach ($binding in $relevantBindings) {
                Write-Host "   ✅ saga.exchange -> saledetail.queue [routing_key: $($binding.routing_key)]" -ForegroundColor Green
            }
        } else {
            Write-Host "   ⚠️  No hay bindings configurados - se crearán al iniciar la app" -ForegroundColor Yellow
        }
    }
    
} catch {
    Write-Host "   ⚠️  No se pudo conectar a la API HTTP (puerto 15672)" -ForegroundColor Yellow
    Write-Host "   Esto es opcional - la app configurará RabbitMQ automáticamente" -ForegroundColor Gray
}

# 4. Resumen final
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "📋 RESUMEN" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "✅ RabbitMQ funcionando correctamente" -ForegroundColor Green
Write-Host "`n🎯 Al iniciar SaleDetail.Api se creará automáticamente:" -ForegroundColor White
Write-Host "   • Exchange: saga.exchange (topic, durable)" -ForegroundColor Gray
Write-Host "   • Cola: saledetail.queue (durable)" -ForegroundColor Gray
Write-Host "   • Bindings:" -ForegroundColor Gray
Write-Host "     - sale.created" -ForegroundColor Gray
Write-Host "     - sale.completed" -ForegroundColor Gray
Write-Host "     - sale.failed" -ForegroundColor Gray
Write-Host "`n🌐 Panel web RabbitMQ: http://localhost:15672" -ForegroundColor Cyan
Write-Host "   Usuario: guest | Password: guest`n" -ForegroundColor Gray
