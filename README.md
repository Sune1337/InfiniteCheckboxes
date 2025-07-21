# Browse the official webpage
https://infinitecheckbox.es/

# HOWTO: Run the app on your local computer
1. Run redis server
2. Run MongoDb server
2. Run silo
3. Run web-app

## Run redis server
```shell
docker run -p 6379:6379 --name checkbox-redis redis
```

## Run MongoDb server
```shell
docker run -p 27017:27017 --name cluster-mongodb -v c:/checkbox_mongodb:/data/db mongo:8
```

## Run silo
You can run more than 1 silo, but 1 is enough for local testing.

```shell
cd <SolutionDir>\SiloHost\bin\Debug\net9.0
.\SiloHost.exe
```

## Run web-app
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

# HOWTO: Build docker images and publish to registry

## SiloHost
```shell
cd <SolutionDir>
docker build -t infinite-checkboxes/silohost:1.0.0 -f SiloHost/Dockerfile .
docker tag infinite-checkboxes/silohost:1.0.0 <Private registry>/infinite-checkboxes/silohost:1.0.0
docker push <Private registry>/infinite-checkboxes/silohost:1.0.0
```

## Webapp
```shell
cd <SolutionDir>
docker build -t infinite-checkboxes/webapp:1.0.0 -f InfiniteCheckboxes/Dockerfile .
docker tag infinite-checkboxes/webapp:1.0.1 <Private registry>/infinite-checkboxes/webapp:1.0.0
docker push <Private registry>/infinite-checkboxes/webapp:1.0.0
```


# HOWTO: Deploy to kubernetes cluster

## Create kubernetes cluster
Do this as you wish. Make sure to download the configuration so you have access to the cluster.

## Setup local env
Make sure your kubectl matches your cluster version according to https://kubernetes.io/docs/tasks/tools/install-kubectl-windows/.

"You must use a kubectl version that is within one minor version difference of your cluster.
For example, a v1.33 client can communicate with v1.32, v1.33, and v1.34 control planes.
Using the latest compatible version of kubectl helps avoid unforeseen issues."

1. Download kubectl and helm to c:\k8s
2. Open a terminal with powershell and verify you are using the correct kubectl
```shell
$env:Path = "c:\k8s;$env:Path"
kubectl version
```
3. Save your kubernetes configuration somewhere and verify connection
```shell
$env:KUBECONFIG="<PATH TO CONFIG>"
kubectl cluster-info
```

## Install an ingress-controller
This example uses Traefik.
Instructions from: https://docs.vultr.com/how-to-install-traefik-ingress-controller-with-cert-manager-on-kubernetes

```shell
kubectl create namespace traefik-namespace
helm repo add traefik https://helm.traefik.io/traefik
helm repo update
helm install --namespace=traefik-namespace traefik traefik/traefik

# Check traefik service and wait for an external IP to be assigned.
kubectl get services -n traefik-namespace

#NAME      TYPE           CLUSTER-IP     EXTERNAL-IP   PORT(S)                      AGE
#traefik   LoadBalancer   10.96.43.157   <WAIT FOR IT> 80:31890/TCP,443:32587/TCP   116s
```

### If you want to enable proxy-protocol
You may need to do this to be able to see clients real ip-addresses in request-logging.  
Create a `traefik-values.yaml` file.
```yaml
ports:
  web:
    port: 8000
    proxyProtocol:
      enabled: true
      trustedIPs:
        - "10.0.0.0/8"
  websecure:
    port: 8443
    proxyProtocol:
      enabled: true
      trustedIPs:
        - "10.0.0.0/8"

service:
  annotations:
    service.beta.kubernetes.io/vultr-loadbalancer-proxy-protocol: 'true'
  spec:
    externalTrafficPolicy: Local
```

Then upgrade the installation.
```shell
helm upgrade --namespace=traefik-namespace traefik traefik/traefik -f traefik-values.yaml
```


## Use letsencrypt to issue certs.
First install cert-manager.
```shell
# Install cert-manager
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.17.2/cert-manager.yaml

# Verify that all cert-manager components are installed and running.
kubectl get pods --namespace cert-manager
```

Create a new file named cluster-issuer.yaml. Replace email with a proper email.
```yaml
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt-prod
spec:
  acme:
    email: hello@example.com
    server: https://acme-v02.api.letsencrypt.org/directory
    privateKeySecretRef:
      name: letsencrypt-prod-key
    solvers:
      - http01:
          ingress:
            class: traefik
```

Apply the cluster-issuer and check status.
```shell
kubectl apply -f cluster-issuer.yaml
kubectl get clusterissuer

#NAME               READY   AGE
#letsencrypt-prod   True    2s
```



## Deploy helm chart to a kubernetes cluster
This is done by installing a helm chart.

Start by creating an override file with your config:

`prod-values.yaml`
```yaml
imageCredentials:
  registry: <Private registry>
  username: "xxxxxx"
  password: "xxxxxx"
  email: ""

images:
  cluster_mongodb:
    persistence:
      enabled: true
      claimName: cluster-mongodb-pvc

  silohost:
    repository: <Private registry>/infinite-checkboxes/silohost

  webapp:
    repository: <Private registry>/infinite-checkboxes/webapp

persistentVolumeClaims:
  cluster-mongodb-pvc:
    enabled: true
    size: 40Gi
    storageClass: <Whatever your cluster supports>
    accessModes:
      - ReadWriteOnce
    annotations:
      "helm.sh/resource-policy": keep

ingress:
  enabled: true
  annotations:
    traefik.ingress.kubernetes.io/router.entrypoints: websecure
  hosts:
    - host: infinite-checkboxes.local
      paths:
        - path: /
          pathType: Prefix
  tls: []
```

1. Create a namespace  
The default namespace is "default", but you should create a separate namespace for the install.
```shell
kubectl create namespace infinite-checkboxes-prod
```

2. Install/upgrade the helm chart
```shell
cd <SolutionDir>\helm
helm upgrade --install infinite-checkboxes-prod .\infinite-checkboxes\ --namespace infinite-checkboxes-prod -f .\prod-values.yaml
```

## Log to Loki
1. Include the serilog configuration in you overrides file.
```yaml
grafanaLokiSecrets:
  Serilog__WriteTo__0__Args__credentials__login: ""
  Serilog__WriteTo__0__Args__credentials__password: ""

  silohost:
    serilog:
      Serilog__Using__0: Serilog.Sinks.Grafana.Loki;
      Serilog__WriteTo__0__Name: GrafanaLoki;
      Serilog__WriteTo__0__Args__uri: <URL TO YOUR LOKI SERVER>
      Serilog__WriteTo__0__Args__labels__0__key: service_name;
      Serilog__WriteTo__0__Args__labels__0__value: silohost;
      Serilog__WriteTo__0__Args__propertiesAsLabels__0: "PodName"
    grafanaLokiSecrets: grafana-loki-secrets
      

  webapp:
    serilog:
      Serilog__Using__0: Serilog.Sinks.Grafana.Loki;
      Serilog__WriteTo__0__Name: GrafanaLoki;
      Serilog__WriteTo__0__Args__uri: <URL TO YOUR LOKI SERVER>
      Serilog__WriteTo__0__Args__labels__0__key: service_name;
      Serilog__WriteTo__0__Args__labels__0__value: webapp;
      Serilog__WriteTo__0__Args__propertiesAsLabels__0: "PodName"
    grafanaLokiSecrets: grafana-loki-secrets
```

# Helm tips & tricks


## dry-run
To see what will be applied to the cluster.
```shell
cd <SolutionDir>\helm
helm install --dry-run --debug infinite-checkboxes .\infinite-checkboxes\
```

## helm diff
To see what differs from a new config to the running.
To get it working on Windows, check: https://github.com/databus23/helm-diff/issues/316
```shell
helm plugin install https://github.com/databus23/helm-diff
helm diff upgrade infinite-checkboxes-prod .\infinite-checkboxes\ --namespace infinite-checkboxes-prod -f .\prod-values.yaml
```


# HOWTO: Install Prometheus kube stack

1. Create a namespace for monitoring
```shell
kubectl create namespace monitoring
```

2. Create an override file for kube-prometheus-stack to get persistent storage 
```yaml
prometheus:
  prometheusSpec:
    storageSpec:
      volumeClaimTemplate:
        metadata:
          annotations:
            "helm.sh/resource-policy": keep
        spec:
          accessModes: ["ReadWriteOnce"]
          resources:
            requests:
              storage: 40Gi
          storageClassName: <Whatever your cluster supports>

grafana:
  persistence:
    enabled: true
    size: 40Gi
    annotations:
      "helm.sh/resource-policy": keep
    storageClassName: <Whatever your cluster supports>

alertmanager:
  alertmanagerSpec:
    storage:
      volumeClaimTemplate:
        metadata:
          annotations:
            "helm.sh/resource-policy": keep
        spec:
          accessModes: ["ReadWriteOnce"]
          resources:
            requests:
              storage: 40Gi
          storageClassName: <Whatever your cluster supports>
```

3. Install kube-prometheus-stack in monitoring namespace
```shell
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo update

# Install with your values file
helm install prometheus prometheus-community/kube-prometheus-stack -f prod-values.yaml --namespace monitoring
```

## Check status
```shell
kubectl --namespace monitoring get pods -l "release=prometheus"
```

## Get Grafana 'admin' user password by running
```shell
# Linux
kubectl --namespace monitoring get secrets prometheus-grafana -o jsonpath="{.data.admin-password}" | base64 -d ; echo

# Powershell
$encoded=$(kubectl --namespace monitoring get secrets prometheus-grafana -o jsonpath="{.data.admin-password}")
[System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($encoded))

```

## Access Grafana local instance
```shell
kubectl port-forward service/prometheus-grafana 3000:80 --namespace monitoring
```

## Create a service-monitor
This is to save metrics from the metrics servers in silohost and webapp containers to Prometheus.
Create a file `servicemonitor.yaml`.
```yaml
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: infinite-checkboxes-metrics-monitor
  # This should be the namespace where kube-prometheus-stack is installed
  namespace: monitoring
  labels:
    # This label is important - it should match the serviceMonitorSelector in your Prometheus CR
    release: prometheus
spec:
  namespaceSelector:
    matchNames:
      - infinite-checkboxes-prod
  selector:
    matchLabels:
      prometheus-metrics-server: "true"
  endpoints:
    - port: metrics  # The port name in your service that exposes metrics
      # interval: 30s  # Optional: scrape interval, defaults to Prometheus global scrape interval
      # path: /metrics  # Optional: metrics path if different from /metrics
```

Apply the file to the k8s cluster and verify it got created.
```shell
kubectl apply -f servicemonitor.yaml
kubectl get servicemonitor -n monitoring
```

The metrics from silohost and webapp should be appearing in Prometheus / Grafana shortly.
