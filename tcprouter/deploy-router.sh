#!bin/bash

########################################################################
# Deployment variables
########################################################################

## Shared variables
RESOURCE_GROUP=test-tcprouter-rg
LOCATION=eastus2
VNET_NAME=tcprouter-vnet
VNET_PREFIXES=10.1.0.0/16
KEYVAULT_NAME=tcprouter-kv

STORAGE_NAME=tcprouterresources
CONTAINER_NAME=scripts

## Management resources
MGMT_SUBNET=mgmt-subnet
MGMT_SUBNET_RANGE=10.1.2.0/24
MGMT_VM_NAME=jumpbox
MGMT_DNS_NAME=masrouterjump
MGMT_USERNAME=jumpboxadmin
MGMT_VM_SIZE=Standard_DS1_v2
MGMT_VM_IMAGE=UbuntuLTS
MGMT_IP_ADDRESS=10.1.2.5

## TCP router resources
ROUTER_SUBNET=router-subnet
ROUTER_SUBNET_RANGE=10.1.3.0/24
ROUTER_VMSS_NAME=tcprouter-vmss
ROUTER_VMSS_IMAGE=UbuntuLTS
ROUTER_USERNAME=routeradmin
ROUTER_INSTANCE_COUNT=1
ROUTER_SKU=Standard_DS4_v2
ROUTER_PORT=5000

########################################################################
# Shared resources
########################################################################

# Deploy the resource group
echo "Creating encapsulating resource group"
az group create --name ${RESOURCE_GROUP} \
    --location ${LOCATION}

az keyvault create --resource-group ${RESOURCE_GROUP} \
    --location ${LOCATION} --sku standard \
    --name ${KEYVAULT_NAME}

if [ ! -f ~/.ssh/tcprouter ]; then
    echo "Creating tcprouter SSH keys"

    # TODO - check to see if the keys exist before regenerating
    ssh-keygen -f ~/.ssh/tcprouter -P ""
fi
SSH_KEYDATA=`cat ~/.ssh/tcprouter.pub`

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

#az storage container create --account-name $STORAGE_NAME \
#    --sas-token $STORAGE_SAS --name $CONTAINER_NAME \
#    --public-access container 

########################################################################
# Create the jumpbox and management resources
########################################################################

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

# Enable ARM access (reader on this resource group) via MSI for this VM
az vm assign-identity --resource-group $RESOURCE_GROUP --name $MGMT_VM_NAME
mgmtvm_spid=$(az resource list --name $MGMT_VM_NAME --resource-group $RESOURCE_GROUP --query [*].identity.principalId --out tsv)
az role assignment create --assignee $mgmtvm_spid --role 'Reader' \
    --resource-group $RESOURCE_GROUP

# Copy the management ssh keys to the server
scp -o StrictHostKeyChecking=no -i ~/.ssh/tcprouter ~/.ssh/tcprouter \
     $MGMT_USERNAME@$MGMT_DNS_NAME.$LOCATION.cloudapp.azure.com:~/.ssh/id_rsa 

# TODO - set up a ELK/grafana/influxdb box with a fixed IP address

echo "alias ssh-router-jumpbox='ssh -o StrictHostKeyChecking=no -i ~/.ssh/tcprouter $MGMT_USERNAME@$MGMT_DNS_NAME.$LOCATION.cloudapp.azure.com'" >> deployment-shortcuts.sh

########################################################################
# Front end TCP proxy
########################################################################

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
    --name allow-incoming-tcp-rule --priority 101 \
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

# TODO - add custom data for cloud package install (haproxy, telegraf, beats)
 #     [--upgrade-policy-mode {Automatic, Manual, Rolling}]       
 #              [--custom-data]

# TODO - update all VMs

