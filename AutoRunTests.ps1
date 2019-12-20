Write-Output "Starting Test"
Get-Date
$env:KUBECONFIG = "$ENV:UserProfile\.kube\k3s.yaml"
kubectl get nodes

Write-Output "manifests-scenario1-steady100"
Get-Date
kubectl apply -f .\Dream-StreamScenarios\manifests-scenario1-steady100
Start-Sleep -s 2400

Write-Output "manifests-scenario10-DB1"
Get-Date
kubectl apply -f .\Dream-StreamScenarios\manifests-scenario10-DB1
Start-Sleep -s 5400

Write-Output "manifests-scenario3-steady33"
Get-Date
kubectl apply -f .\Dream-StreamScenarios\manifests-scenario3-steady33
Start-Sleep -s 1800

Write-Output "manifests-scenario11-DB2"
Get-Date
kubectl apply -f .\Dream-StreamScenarios\manifests-scenario11-DB2
Start-Sleep -s 5400

Write-Output "manifests-scenario2-steady66"
Get-Date
kubectl apply -f .\Dream-StreamScenarios\manifests-scenario2-steady66
Start-Sleep -s 1800

Write-Output "scenario9-10and100every1min"
Get-Date
kubectl apply -f .\Dream-StreamScenarios\manifests-scenario9-10and100every1min
Start-Sleep -s 1800

Write-Output "scenario8-10and100every2min"
Get-Date
kubectl apply -f .\Dream-StreamScenarios\manifests-scenario8-10and100every2min
Start-Sleep -s 1800

Write-Output "scenario7-10and100every5min"
Get-Date
kubectl apply -f .\Dream-StreamScenarios\manifests-scenario7-10and100every5min
Start-Sleep -s 2400

Write-Output "scenario6-10and100every10min"
Get-Date
kubectl apply -f .\Dream-StreamScenarios\manifests-scenario6-10and100every10min
Start-Sleep -s 3600


Write-Output "scenario5-decreasing"
Get-Date
kubectl apply -f .\Dream-StreamScenarios\manifests-scenario5-decreasing
Start-Sleep -s 3000


Write-Output "scenario4-increasing"
Get-Date
kubectl apply -f .\Dream-StreamScenarios\manifests-scenario4-increasing
Start-Sleep -s 3600

Write-Output "Stopped!"
kubectl delete -f .\Dream-StreamScenarios\manifests-scenario5-decreasing

Write-Output "This is the end!"


