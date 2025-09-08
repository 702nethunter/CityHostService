for i in $(seq 1 100); do
  nohup setsid dotnet out/CityWorker.Client.dll --instance "$i" \
    </dev/null >>"logs/client-$i.out" 2>&1 &
done
disown -a
echo "Launched 100 clients."
