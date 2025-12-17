# Microservicio de Detalle de Venta (SaleDetail)

Microservicio para gestionar los detalles de las ventas en el sistema FarmaArquiSoft.

## Estructura del Proyecto

El proyecto sigue una arquitectura limpia (Clean Architecture) con las siguientes capas:

### 📁 SaleDetail.Domain
Capa de dominio que contiene las entidades de negocio, interfaces y excepciones del dominio.

- **Entities/**: Entidad `SaleDetail` con información de la venta
- **Interfaces/**: `ISaleDetailRepository` e `IValidator<T>`
- **Exceptions/**: Excepciones personalizadas del dominio

### 📁 SaleDetail.Application
Capa de aplicación que contiene la lógica de negocio y casos de uso.

- **Services/**: Implementación de la lógica de negocio
- **Validators/**: Validaciones de negocio con FluentResults
- **Interfaces/**: Contratos para servicios y gateways

### 📁 SaleDetail.Infrastructure
Capa de infraestructura que contiene implementaciones de acceso a datos y servicios externos.

- **Repository/**: Implementación del repositorio con MySQL
- **Gateways/**: Comunicación con otros microservicios (Medicines y Sales)
- **Persistences/**: Conexión a base de datos

### 📁 SaleDetail.Api
Capa de presentación - API REST con ASP.NET Core.

- **Controllers/**: Controladores REST
- **Program.cs**: Configuración de servicios y middleware
- **appsettings.json**: Configuración de la aplicación

## Entidad SaleDetail

La entidad principal contiene:

- `id`: Identificador único
- `sale_id`: Referencia al microservicio de Ventas
- `medicine_id`: Referencia al microservicio de Medicinas
- `quantity`: Cantidad de productos
- `unit_price`: Precio unitario
- `total_amount`: Importe total (calculado automáticamente)
- `description`: Descripción del producto
- Campos de auditoría: `created_at`, `updated_at`, `created_by`, `updated_by`, `is_deleted`

## Endpoints de la API

- `POST /api/SaleDetails` - Registrar un nuevo detalle de venta
- `GET /api/SaleDetails` - Obtener todos los detalles de venta
- `GET /api/SaleDetails/{id}` - Obtener un detalle por ID
- `GET /api/SaleDetails/sale/{saleId}` - Obtener todos los detalles de una venta específica
- `PUT /api/SaleDetails/{id}` - Actualizar un detalle de venta
- `DELETE /api/SaleDetails/{id}` - Eliminar (soft delete) un detalle de venta

## Configuración

### Base de Datos

Configurar la cadena de conexión en `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Port=3306;Database=saledetaildb;User Id=root;Password=YOUR_PASSWORD;"
}
```

### Script SQL para crear la tabla:

```sql
CREATE DATABASE IF NOT EXISTS saledetaildb;
USE saledetaildb;

CREATE TABLE sale_details (
    id INT AUTO_INCREMENT PRIMARY KEY,
    sale_id INT NOT NULL,
    medicine_id INT NOT NULL,
    quantity INT NOT NULL,
    unit_price DECIMAL(10, 2) NOT NULL,
    total_amount DECIMAL(10, 2) NOT NULL,
    description VARCHAR(200) DEFAULT '',
    is_deleted BOOLEAN DEFAULT 0,
    created_at DATETIME NOT NULL,
    updated_at DATETIME NULL,
    created_by INT NULL,
    updated_by INT NULL,
    INDEX idx_sale_id (sale_id),
    INDEX idx_medicine_id (medicine_id)
);
```

### Microservicios Dependientes

Este microservicio se comunica con:

1. **Microservicio de Medicines** (Puerto 5143): Para validar la existencia de medicamentos
2. **Microservicio de Sales** (Puerto 5000): Para validar la existencia de ventas

Configurar las URLs en `Program.cs`:

```csharp
builder.Services.AddHttpClient("MedicinesApi", client =>
{
    client.BaseAddress = new Uri("http://localhost:5143/");
});

builder.Services.AddHttpClient("SalesApi", client =>
{
    client.BaseAddress = new Uri("http://localhost:5000/");
});
```

### Autenticación JWT

El microservicio utiliza JWT Bearer Token para autenticación. Configurar en `appsettings.json`:

```json
"Jwt": {
  "Key": "eLlO3LhzXDvWFxiMBsmg2zCir49SRD3xCdh2IfuptfI=",
  "Issuer": "FarmaArquiSoft",
  "Audience": "FarmaArquiSoftClients",
  "ExpiresMinutes": 60
}
```

## Ejecutar el Proyecto

```bash
cd MicroServiceSaleDetail/SaleDetail.Api
dotnet restore
dotnet run
```

El API estará disponible en: `http://localhost:5200`

Swagger UI: `http://localhost:5200/swagger`

## Tecnologías Utilizadas

- **.NET 8.0**
- **ASP.NET Core Web API**
- **MySQL** con MySql.Data
- **FluentResults** para manejo de resultados
- **JWT Bearer** para autenticación
- **Swagger/OpenAPI** para documentación

## Validaciones

El servicio valida:
- ✅ Sale ID y Medicine ID válidos
- ✅ Cantidad mayor a cero
- ✅ Precio unitario mayor a cero
- ✅ Existencia de la venta en el microservicio de Sales
- ✅ Existencia del medicamento en el microservicio de Medicines
- ✅ Cálculo automático del importe total
