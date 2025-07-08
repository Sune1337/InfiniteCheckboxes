# HOWTO: Run the app on your local computer
1. Run redis server
2. Run silo
3. Run web-app

## HOWTO: Run redis server
```shell
docker run -p 6379:6379 --name checkbox-redis -v C:/checkbox_redis:/data redis
```

## HOWTO: Run silo
You can run more than 1 silo, but 1 is enough for local testing.

```shell
cd <SolutionDir>\SiloHost\bin\Debug\net9.0
.\SiloHost.exe
```

## HOWTO: Run web-app
1. Start the API-server  
You can either just run the "InfiniteCheckboxes: https" from the IDE or run it from the shell.

```shell
cd <SolutionDir>\InfiniteCheckboxes
dotnet run InfiniteCheckboxes --launch-profile https
```

2. Start the web-app
```shell
cd <SolutionDir>\InfiniteCheckboxes\ClientApp
npm run start
```
