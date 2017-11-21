#!/bin/bash

########################################################################
# Deployment variables
########################################################################
## Shared variables
declare -r RESOURCE_GROUP=test-tcprouter-rg
declare -r LOCATION=eastus2
declare -r VNET_NAME=tcprouter-vnet
declare -r VNET_PREFIXES=10.1.0.0/16
declare -r KEYVAULT_NAME=tcprouter-kv

declare -r STORAGE_NAME=tcprouterresources
declare -r CONTAINER_NAME=scripts

## Management resources
declare -r MGMT_SUBNET=mgmt-subnet
declare -r MGMT_SUBNET_RANGE=10.1.2.0/24
declare -r MGMT_VM_NAME=jumpbox
declare -r MGMT_DNS_NAME=masrouterjump
declare -r MGMT_USERNAME=jumpboxadmin
declare -r MGMT_VM_SIZE=Standard_DS1_v2
declare -r MGMT_VM_IMAGE=UbuntuLTS
declare -r MGMT_IP_ADDRESS=10.1.2.5

## TCP router resources
declare -r ROUTER_SUBNET=router-subnet
declare -r ROUTER_SUBNET_RANGE=10.1.3.0/24
declare -r ROUTER_VMSS_NAME=tcprouter-vmss
declare -r ROUTER_VMSS_IMAGE=UbuntuLTS
declare -r ROUTER_USERNAME=routeradmin
declare -r ROUTER_INSTANCE_COUNT=1
declare -r ROUTER_SKU=Standard_DS4_v2
declare -r ROUTER_PORT=5000

declare -r TCP_POOL_USERNAME=tcpserveradmin
declare -r TCP_POOL_SKU=Standard_DS4_v2
declare -r TCP_POOL_IMAGE=UbuntuLTS
declare -r TCP_POOL_LISTEN_PORT=5000

declare -r TCP_POOL1_SUBNET=pool1-subnet
declare -r TCP_POOL1_SUBNET_RANGE=10.1.4.0/24
declare -r TCP_POOL1_VMSS_NAME=pool1-vmss
declare -r TCP_POOL1_INSTANCE_COUNT=1

declare -r TCP_POOL2_SUBNET=pool2-subnet
declare -r TCP_POOL2_SUBNET_RANGE=10.1.5.0/24
declare -r TCP_POOL2_VMSS_NAME=pool2-vmss
declare -r TCP_POOL2_INSTANCE_COUNT=1

if [ ! -f ~/.ssh/tcprouter ]; then
    echo "Creating tcprouter SSH keys"

    # TODO - check to see if the keys exist before regenerating
    ssh-keygen -f ~/.ssh/tcprouter -P ""
fi
declare -r SSH_KEYDATA=$(cat ~/.ssh/tcprouter.pub)
 
########################################################################
# Shared resources
########################################################################
register_load_balancer_preview() {
    # TEMP - Register for the Load Balancer 
    echo "Registering subscription for load balancer preview" 
    az feature register --name AllowLBPreview --namespace Microsoft.Network

    registered="Registering"
    while [ "$registered" = "Registering" ]; do
        sleep 30
        echo -n "Checking for registration status: "
        registered=$(az feature show --name AllowLBPreview --namespace Microsoft.Network | jq .properties.state | tr -d '"')
        echo $registered 
    done

    echo "Re-registering network provider"
    az provider register --namespace Microsoft.Network
}

deploy_shared_resources() {
    # Deploy the resource group
    echo "Creating encapsulating resource group"
    az group create --name ${RESOURCE_GROUP} \
        --location ${LOCATION}

    az keyvault create --resource-group ${RESOURCE_GROUP} \
        --location ${LOCATION} --sku standard \
        --name ${KEYVAULT_NAME}
    az keyvault secret set --vault-name ${KEYVAULT_NAME} \
        --name tcprouter-ssh --file  ~/.ssh/tcprouter
    az keyvault secret set --vault-name ${KEYVAULT_NAME} \
        --name tcprouter-ssh-pub --file  ~/.ssh/tcprouter.pub

    # Create the resource  VNET
    az network vnet create --resource-group ${RESOURCE_GROUP} \
        --name ${VNET_NAME} --address-prefixes ${VNET_PREFIXES} 

    # Create a storage account for deployment resources
    az storage account create --resource-group ${RESOURCE_GROUP} \
        --name ${STORAGE_NAME} \
        --sku Standard_LRS \
        --location ${LOCATION} \
        --kind BlobStorage \
        --access-tier Hot

    STORAGE_KEY=`az storage account keys list --resource-group $RESOURCE_GROUP \
        --account-name $STORAGE_NAME | jq .[0].value | tr -d '"'`

    STORAGE_SAS=`az storage account generate-sas --account-name $STORAGE_NAME \
        --account-key $STORAGE_KEY --permissions acdlpruw --resource-types sco \
        --services b --expiry 2019-03-01T00:00Z | tr -d '"'`
    az keyvault secret set --vault-name ${KEYVAULT_NAME} \
        --name storage-sas --value "${STORAGE_SAS}"

    # TODO - lock the storage account down to the mgmt subnets
    # TODO _ nsg to allow access to 
    #az storage container create --account-name $STORAGE_NAME \
    #    --sas-token $STORAGE_SAS --name $CONTAINER_NAME \
    #    --public-access container 
}

########################################################################
# Create the jumpbox and management resources
########################################################################
deploy_mgmt_resources()
{
    az network vnet subnet create --resource-group ${RESOURCE_GROUP} \
        --vnet-name $VNET_NAME --name $MGMT_SUBNET \
        --address-prefix $MGMT_SUBNET_RANGE

    az vm create --resource-group $RESOURCE_GROUP --name $MGMT_VM_NAME \
        --location $LOCATION --image $MGMT_VM_IMAGE \
        --admin-username $MGMT_USERNAME --ssh-key-value "${SSH_KEYDATA}" \
        --authentication-type ssh \
        --size $MGMT_VM_SIZE \
        --storage-sku Premium_LRS \
        --vnet-name $VNET_NAME \
        --subnet $MGMT_SUBNET \
        --public-ip-address-dns-name $MGMT_DNS_NAME \
        --private-ip-address $MGMT_IP_ADDRESS \
        --custom-data jumpbox-cloud-init.txt \
        --data-disk-sizes-gb 1024

    az vm open-port --resource-group $RESOURCE_GROUP --name $MGMT_VM_NAME \
        --port 3000 --priority 100
    az vm open-port --resource-group $RESOURCE_GROUP --name $MGMT_VM_NAME \
        --port 5601 --priority 101

    # Enable ARM access (reader on this resource group) via MSI for this VM
    az vm assign-identity --resource-group $RESOURCE_GROUP --name $MGMT_VM_NAME
    mgmtvm_spid=$(az resource list --name $MGMT_VM_NAME --resource-group $RESOURCE_GROUP --query [*].identity.principalId --out tsv)
    az role assignment create --assignee $mgmtvm_spid --role 'Reader' \
        --resource-group $RESOURCE_GROUP

    # Copy the management ssh keys to the server
    scp -o StrictHostKeyChecking=no -i ~/.ssh/tcprouter ~/.ssh/tcprouter \
        $MGMT_USERNAME@$MGMT_DNS_NAME.$LOCATION.cloudapp.azure.com:~/.ssh/id_rsa 

    echo "alias ssh-router-jumpbox='ssh -o StrictHostKeyChecking=no -i ~/.ssh/tcprouter $MGMT_USERNAME@$MGMT_DNS_NAME.$LOCATION.cloudapp.azure.com'" >> deployment-shortcuts.sh
    alias ssh-router-jumpbox='ssh -o StrictHostKeyChecking=no -i ~/.ssh/tcprouter $MGMT_USERNAME@$MGMT_DNS_NAME.$LOCATION.cloudapp.azure.com'
}

########################################################################
# Front end TCP proxy
########################################################################
deploy_frontend_proxy() {
    # Create the front end router subnet  
    az network vnet subnet create --resource-group ${RESOURCE_GROUP} \
        --vnet-name ${VNET_NAME} --name $ROUTER_SUBNET \
        --address-prefix $ROUTER_SUBNET_RANGE

    # Create the network security group and rules
    az network nsg create --resource-group $RESOURCE_GROUP --location $LOCATION \
        --name router-nsg 

    az network nsg rule create --resource-group $RESOURCE_GROUP --nsg-name router-nsg \
        --name allow-incoming-tcp-rule --priority 100 \
        --description "Allow incoming connections to $ROUTER_PORT" \
        --protocol tcp --access Allow --direction Inbound \
        --destination-port-ranges $ROUTER_PORT

    az network nsg rule create --resource-group $RESOURCE_GROUP --nsg-name router-nsg \
        --name allow-incoming-ssh --priority 101 \
        --description "Allow incoming ssh connections" \
        --protocol tcp --access Allow --direction Inbound \
        --destination-port-ranges 22

    # TODO - find out why the vnet / subnet names don't work
    # Create the VMSS pool
    mgmt_subnetid=$(az network vnet subnet show --resource-group ${RESOURCE_GROUP} --vnet-name ${VNET_NAME} --name $MGMT_SUBNET --query id --output tsv)
    az vmss create --resource-group $RESOURCE_GROUP \
        --name $ROUTER_VMSS_NAME --location $LOCATION \
        --image $ROUTER_VMSS_IMAGE --vm-sku $ROUTER_SKU \
        --instance-count $ROUTER_INSTANCE_COUNT \
        --ssh-key-value "${SSH_KEYDATA}" \
        --subnet $mgmt_subnetid \
        --storage-sku Premium_LRS \
        --accelerated-networking \
        --admin-username $ROUTER_USERNAME \
        --assign-identity \
        --accelerated-networking \
        --nsg router-nsg \
        --upgrade-policy-mode automatic \
        --custom-data tcprouter-cloud-init.txt

    # TODO - pre-create the load balancer and assign to the VMSS
    # Create a a public IP
    az network public-ip create --resource-group $RESOURCE_GROUP \
        --allocation-method static \
        --location $LOCATION \
        --name tcprouter-pip

    az network lb frontend-ip create --resource-group $RESOURCE_GROUP \
        --lb-name ${ROUTER_VMSS_NAME}LB \
        --name tcprouter-fe \
        --public-ip-address tcprouter-pip

    # Configure the load balancer
    az network lb rule create --resource-group $RESOURCE_GROUP \
        --lb-name ${ROUTER_VMSS_NAME}LB \
        --name allow-tcp-traffic-rule \
        --backend-pool-name ${ROUTER_VMSS_NAME}LBBEPool \
        --backend-port $ROUTER_PORT \
        --frontend-ip-name  tcprouter-fe \
        --frontend-port $ROUTER_PORT \
        --protocol tcp 
    
    # TODO - add custom data for cloud package install (haproxy, telegraf, beats)
    #     [--upgrade-policy-mode {Automatic, Manual, Rolling}]       
    #              [--custom-data]

    # TODO - update all VMs
}

########################################################################
# TCP Server pool #1
########################################################################
deploy_tcpserver_pool() {
    # Create the front end router subnet  
    az network vnet subnet create --resource-group ${RESOURCE_GROUP} \
        --vnet-name ${VNET_NAME} --name $TCP_POOL1_SUBNET \
        --address-prefix $TCP_POOL1_SUBNET_RANGE

    # Create the network security group and rules
    az network nsg create --resource-group $RESOURCE_GROUP --location $LOCATION \
        --name tcp-pool1-nsg 

    az network nsg rule create --resource-group $RESOURCE_GROUP --nsg-name tcp-pool1-nsg \
        --name allow-incoming-tcp-rule --priority 100 \
        --description "Allow incoming connections to $ROUTER_PORT" \
        --protocol tcp --access Allow --direction Inbound \
        --destination-port-ranges $TCP_POOL_LISTEN_PORT

    az network nsg rule create --resource-group $RESOURCE_GROUP --nsg-name tcp-pool1-nsg \
        --name allow-incoming-ssh --priority 101 \
        --description "Allow incoming ssh connections" \
        --protocol tcp --access Allow --direction Inbound \
        --destination-port-ranges 22

    # TODO - find out why the vnet / subnet names don't work
    # Create the VMSS pool
    pool1_subnetid=$(az network vnet subnet show --resource-group ${RESOURCE_GROUP} --vnet-name ${VNET_NAME} --name $TCP_POOL1_SUBNET --query id --output tsv)
    az vmss create --resource-group $RESOURCE_GROUP \
        --name $TCP_POOL1_VMSS_NAME --location $LOCATION \
        --image $TCP_POOL_IMAGE --vm-sku $TCP_POOL_SKU \
        --instance-count $TCP_POOL1_INSTANCE_COUNT \
        --ssh-key-value "${SSH_KEYDATA}" \
        --subnet $pool1_subnetid \
        --storage-sku Premium_LRS \
        --accelerated-networking \
        --admin-username $TCP_POOL_USERNAME \
        --assign-identity \
        --accelerated-networking \
        --nsg tcp-pool1-nsg \
        --upgrade-policy-mode automatic \
        --custom-data tcpserver-cloud-init.txt
}

main()
{
    deploy_shared_resources
    deploy_mgmt_resources
    deploy_frontend_proxy
    deploy_tcpserver_pool
}

main
