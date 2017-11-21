#!/bin/bash

# Usage: 
# dump-vmid.sh [csv with list of subscriptions]

echo -n "Retrieving current subscription: " 
OLDSUB=$(az account show --query id --output tsv)
echo $OLDSUB

echo "Writing vmids to file vmlist.csv"
echo "name,resourceGroup,vmid,armid" > vmlist.csv

for guid in $(cat $1); do 
    echo -n "Pulling VM information for subscription $guid: "

    az account set --subscription ${guid}
    az vm list | jq '.[] | "\(.name),\(.resourceGroup),\(.vmId),\(.id)"' | tr -d '"' >>  vmlist.csv

    echo "done"
done

exit

echo -n "Setting subscription back to ${OLDSUB}"
az account set --subscription ${OLDSUB}
echo "done"