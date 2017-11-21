#!/bin/bash

 az vmss nic list --vmss-name tcprouter-vmss \
     --resource-group test-tcprouter-rg \
     | jq '.[].ipConfigurations | .[].privateIpAddress' \
     | tr -d '"' 
