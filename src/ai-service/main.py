from fastapi import FastAPI
from opentelemetry import trace
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor
from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import OTLPSpanExporter
from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor
from opentelemetry.instrumentation.logging import LoggingInstrumentor
import threading
import os
from task_consumer import TaskConsumer

# Configure OpenTelemetry
trace.set_tracer_provider(TracerProvider())
trace.get_tracer_provider().add_span_processor(
    BatchSpanProcessor(OTLPSpanExporter())
)

app = FastAPI()

# Instrument FastAPI
FastAPIInstrumentor.instrument_app(app)
LoggingInstrumentor().instrument(set_logging_format=True)

# Start RabbitMQ consumer in background thread
consumer = None

@app.on_event("startup")
async def startup_event():
    global consumer
    consumer = TaskConsumer()
    consumer_thread = threading.Thread(target=consumer.start, daemon=True)
    consumer_thread.start()
    print("✓ RabbitMQ consumer started in background")

@app.on_event("shutdown")
async def shutdown_event():
    if consumer:
        consumer.stop()

@app.get("/")
def read_root():
    return {
        "message": "AI Service is running!",
        "status": "healthy",
        "features": ["sentiment_analysis", "task_categorization"]
    }

@app.get("/health")
def health_check():
    return {
        "status": "healthy",
        "consumer_running": consumer is not None and consumer.is_running if consumer else False
    }

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)