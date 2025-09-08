# See what's running
pgrep -fa 'CityWorker.Client.dll --instance'

# Stop all client instances
pkill -f 'CityWorker.Client.dll --instance'

# Verify they’re gone
pgrep -fa 'CityWorker.Client.dll --instance' || echo "All clients stopped."
