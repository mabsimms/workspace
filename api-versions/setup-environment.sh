#!/bin/bash

default_location='eastus'
default_keyvault='sharedkv'
default_resource_group='sharedkv-rg'

# Allow service principal to read but not make any changes
default_role='Reader'

sp_keys=(AZURE_SUBSCRIPTION_ID AZURE_CLIENT_ID AZURE_TENANT_ID AZURE_CLIENT_SECRET)
variables_set=0

check_variables() { 
    echo "Checking variables"

    # Check to see if azure service principal environment variables are set
    if [ ! -z "$AZURE_SUBSCRIPTION_ID" ]; then
        echo "AZURE_SUBSCRIPTION_ID is set to $AZURE_SUBSCRIPTION_ID"
    else
        variables_set=1
    fi

    if [ ! -z "$AZURE_CLIENT_ID" ]; then
        echo "AZURE_CLIENT_ID is set to $AZURE_CLIENT_ID"
    else
        variables_set=1
    fi

    if [ ! -z "$AZURE_TENANT_ID" ]; then
        echo "AZURE_TENANT_ID is set to $AZURE_TENANT_ID"
    else
        variables_set=1
    fi

    if [ ! -z "$AZURE_CLIENT_SECRET" ]; then
        echo "AZURE_CLIENT_SECRET is set"
    else
        variables_set=1
    fi

    if [ $variables_set == 0 ]; then
        echo "Environmental variables set correctly"
        return 0
    else
        echo "Azure service principal environment variables not set"
        return -1
    fi
}

function check_keyvault_exists() { 
    # If not, check to see if an azure keyvault is available and contains
    # the service principal
    echo "Checking for service principal registered in keyvault"
    if [ -z "$AZURE_KEYVAULT" ]; then
        echo "Azure Keyvault not set - assigning default $default_keyvault"
        export AZURE_KEYVAULT=$default_keyvault
    fi

    echo "Azure Keyvault set as $AZURE_KEYVAULT"
    keyvault_exists=$(az keyvault list --query "length([?name == '$AZURE_KEYVAULT'])")
    if [ $keyvault_exists != 1 ]; then 
        echo "shared keyvault $AZURE_KEYVAULT does not exist; creating"
        az group create --name $default_resource_group --location $default_location
        az keyvault create --resource-group $default_resource_group \ 
            --name $AZURE_KEYVAULT \
            --location $default_location \
            --sku standard
        # TODO - create an access policy
        # https://docs.microsoft.com/en-us/azure/key-vault/key-vault-manage-with-cli2
        # az keyvault set-policy
        echo "Shared keyvault created"
    fi
}

function check_keyvault_values() { 
    # Check values from keyvault
    echo "Checking if variables registered in keyvault"
    export AZURE_SUBSCRIPTION_ID=$(az keyvault secret show --vault-name $AZURE_KEYVAULT --name AZURESUBSCRIPTIONID --query value -o tsv 2>/dev/null)
    export AZURE_CLIENT_ID=$(az keyvault secret show --vault-name $AZURE_KEYVAULT --name AZURECLIENTID --query value -o tsv 2>/dev/null)
    export AZURE_TENANT_ID=$(az keyvault secret show --vault-name $AZURE_KEYVAULT --name AZURETENANTID --query value -o tsv 2>/dev/null)
    export AZURE_CLIENT_SECRET=$(az keyvault secret show --vault-name $AZURE_KEYVAULT --name AZURECLIENTSECRET --query value -o tsv 2>/dev/null)

    # Ensure they are all set correctly
    if [ -z "${AZURE_SUBSCRIPTION_ID}" ] || [ -z "${AZURE_CLIENT_ID}" ] || [ -z "${AZURE_TENANT_ID}" ] || [ -z "${AZURE_CLIENT_SECRET}" ]; then     
        echo "Variables are not set"
        return -1
    else
        return 0
    fi
}

function create_service_principal() { 
    echo "Creating new service principal for use with python"

    # Otherwise, create and register a service principal in keyvault
    export AZURE_SUBSCRIPTION_ID=$(az account show --query id -o tsv)
    echo "Creating service principal for python usage ($default_role role)"
    az ad sp create-for-rbac --role="$default_role" \
            --scopes="/subscriptions/$AZURE_SUBSCRIPTION_ID" > adrole.json
    export AZURE_CLIENT_ID=$(cat adrole.json | jq .appId | tr -d '"')
    export AZURE_CLIENT_SECRET=$(cat adrole.json | jq .password | tr -d '"')
    export AZURE_TENANT_ID=$(cat adrole.json | jq .tenant | tr -d '"')
    echo "Service principal app id = $AZURE_CLIENT_ID"
    rm adrole.json

    az keyvault secret set --vault-name $AZURE_KEYVAULT --name AZURESUBSCRIPTIONID \
        --value $AZURE_SUBSCRIPTION_ID
    az keyvault secret set --vault-name $AZURE_KEYVAULT --name AZURECLIENTID \
        --value $AZURE_CLIENT_ID
    az keyvault secret set --vault-name $AZURE_KEYVAULT --name AZURECLIENTSECRET \
        --value $AZURE_CLIENT_SECRET
    az keyvault secret set --vault-name $AZURE_KEYVAULT --name AZURETENANTID \
        --value $AZURE_TENANT_ID    
}

check_variables
if [ $? == 0 ]; then 
   return 0
   echo "Variables set"
fi

check_keyvault_exists

check_keyvault_values
if [ $? == 0 ]; then 
   return 0
   echo "Values exist"
fi

#create_service_principal

# TODO - array based code to shorten/simplify
#for key in ${sp_keys[@]}; do
#    echo "Looking up registered value for $key"
#fi
