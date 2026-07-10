FROM python:3.11-slim

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY main.py .

EXPOSE 8090

# ddtrace-run wraps the app with Datadog APM auto-instrumentation
ENTRYPOINT ["ddtrace-run", "python", "main.py"]
