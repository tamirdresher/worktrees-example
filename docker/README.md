# Docker Compose Setup for Complex App

This directory contains the Docker Compose configuration for running the Complex App comparison example. This setup allows you to run the entire application stack (Frontend, Backend, AI Service, PostgreSQL, Redis, RabbitMQ) using Docker.

## Prerequisites

Before you begin, ensure you have the following installed on your machine:

*   [Docker Desktop](https://www.docker.com/products/docker-desktop) (includes Docker Compose)

## Environment Variables

The `docker-compose.yml` file uses environment variables to configure ports and database credentials. These variables are defined in a `.env` file.

| Variable | Description | Default Value |
| :--- | :--- | :--- |
| `FRONTEND_PORT` | Port for the Frontend service | `3000` |
| `BACKEND_PORT` | Port for the Backend API | `5000` |
| `AI_SERVICE_PORT` | Port for the AI Service | `8000` |
| `POSTGRES_USER` | PostgreSQL username | `postgres` |
| `POSTGRES_PASSWORD` | PostgreSQL password | `postgres` |
| `POSTGRES_DB` | PostgreSQL database name | `notetakerdb` |
| `POSTGRES_PORT` | Port for PostgreSQL | `5432` |
| `REDIS_PORT` | Port for Redis | `6379` |
| `RABBITMQ_PORT` | Port for RabbitMQ | `5672` |
| `RABBITMQ_MGMT_PORT` | Port for RabbitMQ Management UI | `15672` |

## Setup Steps

1.  **Navigate to the docker directory:**

    ```bash
    cd complex-comparison/docker
    ```

2.  **Create the `.env` file:**

    Copy the provided example file to create your local configuration.

    ```bash
    cp .env.example .env
    ```

    You can now edit the `.env` file if you need to change any ports or credentials to avoid conflicts with other running services.

3.  **Run the application:**

    Start all services in detached mode (background):

    ```bash
    docker-compose up -d
    ```

    To see the logs of all services:

    ```bash
    docker-compose logs -f
    ```

4.  **Access the application:**

    *   **Frontend:** [http://localhost:3000](http://localhost:3000) (or your configured `FRONTEND_PORT`)
    *   **Backend API:** [http://localhost:5000](http://localhost:5000) (or your configured `BACKEND_PORT`)
    *   **RabbitMQ Management:** [http://localhost:15672](http://localhost:15672) (or your configured `RABBITMQ_MGMT_PORT`) - Default login: `guest` / `guest`

## Troubleshooting

### Port Conflicts

If you encounter an error stating that a port is already allocated (e.g., `Bind for 0.0.0.0:5432 failed: port is already allocated`), it means another service on your machine is using that port.

**Solution:**
1.  Open your `.env` file.
2.  Change the conflicting port to a different value (e.g., change `POSTGRES_PORT=5432` to `POSTGRES_PORT=5433`).
3.  Restart the containers: `docker-compose up -d`

### Database Connection Issues

If the backend or AI service fails to connect to the database, ensure that the `postgres` container is healthy and running. You can check the status with:

```bash
docker-compose ps
```

If the database takes a while to start, the dependent services might fail initially. Docker Compose is configured with `depends_on`, but sometimes a manual restart of the specific service is needed if the database initialization is slow.

```bash
docker-compose restart backend ai-service