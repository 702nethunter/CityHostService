# Run 100 instances in sequence (one after another)
for i in {1..100}; do
  podman run -d --rm --pod cityworker-pod \
    --name "cityworker-client-$i" \
    -e CLIENT_ID="client-$i" \
    -e INSTANCE_NUMBER="$i" \
    -e SERVER_URL="https://localhost:5003" \
    -e BYPASS_SSL_VALIDATION="true" \
    cityworker-client:latest
done

echo "All 100 clients started in background"