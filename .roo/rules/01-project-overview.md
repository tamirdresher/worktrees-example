# NoteTaker - Project Overview

## Project Purpose
NoteTaker is a polyglot cloud-native application designed to demonstrate .NET Aspire orchestration. It allows users to create tasks/notes which are then asynchronously processed by an AI service to analyze sentiment and categorize content.

## Technology Stack

### Backend (API Service)
- **.NET 10.0** with ASP.NET Core Minimal APIs
- **PostgreSQL** for data persistence (via EF Core)
- **Redis** for caching
- **RabbitMQ** for asynchronous messaging
- **OpenTelemetry** for observability

### Frontend (Web)
- **Node.js** with Express
- **OpenTelemetry** for distributed tracing

### AI Service
- **Python** with FastAPI
- **TextBlob** for sentiment analysis and noun phrase extraction
- **Pika** for RabbitMQ integration

### Orchestration
- **.NET Aspire** for local development orchestration, service discovery, and configuration management

## Solution Structure

```
src/
├── backend/                  # .NET Core Minimal API
├── frontend/                 # Node.js Express frontend
├── ai-service/               # Python AI processing service
├── NoteTaker.AppHost/        # .NET Aspire orchestrator
├── NoteTaker.ServiceDefaults/# Shared service configurations
└── NoteTaker.Tests/          # Integration tests
```

## Key Domain Concepts

### 1. Task Item
- Represents a user's note or task
- Contains title, description, status, and AI analysis results
- Stored in PostgreSQL

### 2. Asynchronous Processing
- Tasks are created in "pending" state
- A message is published to RabbitMQ
- AI Service consumes the message, analyzes the text, and updates the task
- Frontend polls or refreshes to see updated status

## Architecture Patterns

### Minimal API
- The backend uses ASP.NET Core Minimal APIs for lightweight and fast endpoints.

### Event-Driven Architecture
- Decoupling between task creation and processing using RabbitMQ.

### Polyglot Microservices
- Demonstrates integration between .NET, Node.js, and Python services running together.

## Configuration Management

### Environment-Specific Settings
- `appsettings.json` / `appsettings.Development.json` for .NET projects
- Environment variables for Node.js and Python services, injected by Aspire

## Observability
- **OpenTelemetry** is configured across all services (dotnet, node, python)
- Traces, metrics, and logs are collected and visualized in the Aspire Dashboard