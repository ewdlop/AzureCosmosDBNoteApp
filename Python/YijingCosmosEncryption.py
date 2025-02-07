```python
from azure.cosmos import CosmosClient, PartitionKey
from azure.cosmos.encryption import (
    CosmosEncryptionClient,
    ClientEncryptionPolicy,
    EncryptionKeyStoreProvider,
    EncryptionType,
    PathsToEncrypt
)
from azure.keyvault.keys import KeyClient
from azure.identity import DefaultAzureCredential
from enum import Enum
from typing import Dict, Any, List
from datetime import datetime
import logging

class YijingState(Enum):
    PEACE = "泰"    # Normal operations
    DANGER = "否"   # Enhanced security
    CHANGE = "革"   # Key rotation
    RETURN = "復"   # Recovery mode
    ADVANCE = "晉"  # Progressive enhancement

class TrigramKeyPolicy(Enum):
    QIAN = {        # Heaven - Master encryption
        "paths": ["/sensitive"],
        "algo": EncryptionType.RANDOMIZED
    }
    KUN = {         # Earth - Foundation data
        "paths": ["/baseline"],
        "algo": EncryptionType.DETERMINISTIC
    }
    ZHEN = {        # Thunder - Dynamic data
        "paths": ["/volatile"],
        "algo": EncryptionType.RANDOMIZED
    }
    XUN = {         # Wind - Flexible data
        "paths": ["/variable"],
        "algo": EncryptionType.RANDOMIZED
    }
    KAN = {         # Water - Flowing data
        "paths": ["/streaming"],
        "algo": EncryptionType.DETERMINISTIC
    }
    LI = {          # Fire - Critical data
        "paths": ["/critical"],
        "algo": EncryptionType.RANDOMIZED
    }
    GEN = {         # Mountain - Stable data
        "paths": ["/stable"],
        "algo": EncryptionType.DETERMINISTIC
    }
    DUI = {         # Lake - Reflective data
        "paths": ["/audit"],
        "algo": EncryptionType.DETERMINISTIC
    }

class YijingCosmosEncryption:
    def __init__(
        self,
        cosmos_endpoint: str,
        key_vault_url: str,
        database_id: str,
        container_id: str
    ):
        self.credential = DefaultAzureCredential()
        self.key_client = KeyClient(vault_url=key_vault_url, credential=self.credential)
        self.current_state = YijingState.PEACE
        self.logger = logging.getLogger("YijingCosmos")
        
        # Initialize encryption provider
        self.key_provider = self._initialize_key_provider(key_vault_url)
        
        # Create encrypted client
        self.cosmos_client = CosmosEncryptionClient(
            url=cosmos_endpoint,
            credential=self.credential,
            key_store_provider=self.key_provider
        )
        
        # Get database and container
        self.database = self.cosmos_client.get_database_client(database_id)
        self.container = self.database.get_container_client(container_id)

    def _initialize_key_provider(self, key_vault_url: str) -> EncryptionKeyStoreProvider:
        """Initialize the key provider with Azure Key Vault"""
        return EncryptionKeyStoreProvider(
            key_vault_url=key_vault_url,
            credential=self.credential
        )

    def _get_encryption_policy(self, state: YijingState) -> ClientEncryptionPolicy:
        """Get encryption policy based on current state"""
        paths_to_encrypt: List[PathsToEncrypt] = []
        
        # Add base paths for all states
        for trigram in TrigramKeyPolicy:
            policy = trigram.value
            paths_to_encrypt.extend([
                PathsToEncrypt(
                    path=path,
                    encryption_type=policy["algo"],
                    encryption_algorithm="AEAD_AES_256_CBC_HMAC_SHA_256"
                ) for path in policy["paths"]
            ])
        
        # Add additional paths based on state
        if state in [YijingState.DANGER, YijingState.CHANGE]:
            # Enhance encryption in heightened states
            paths_to_encrypt.extend([
                PathsToEncrypt(
                    path="/metadata",
                    encryption_type=EncryptionType.RANDOMIZED,
                    encryption_algorithm="AEAD_AES_256_CBC_HMAC_SHA_256"
                )
            ])
        
        return ClientEncryptionPolicy(
            paths_to_encrypt=paths_to_encrypt
        )

    async def create_item(self, item: Dict[str, Any]) -> Dict[str, Any]:
        """Create an encrypted item in the container"""
        # Add metadata based on current state
        item["_metadata"] = {
            "encryption_state": self.current_state.value,
            "timestamp": datetime.utcnow().isoformat()
        }
        
        # Get encryption policy for current state
        policy = self._get_encryption_policy(self.current_state)
        
        try:
            result = await self.container.create_item(
                body=item,
                encryption_policy=policy
            )
            self.logger.info(f"Created encrypted item with id: {item.get('id')}")
            return result
        except Exception as e:
            self.logger.error(f"Error creating encrypted item: {str(e)}")
            raise

    async def read_item(self, item_id: str, partition_key: Any) -> Dict[str, Any]:
        """Read and decrypt an item from the container"""
        try:
            result = await self.container.read_item(
                item=item_id,
                partition_key=partition_key
            )
            self.logger.info(f"Read encrypted item with id: {item_id}")
            return result
        except Exception as e:
            self.logger.error(f"Error reading encrypted item: {str(e)}")
            raise

    async def update_item(self, item: Dict[str, Any]) -> Dict[str, Any]:
        """Update an encrypted item"""
        # Update metadata
        item["_metadata"] = {
            "encryption_state": self.current_state.value,
            "timestamp": datetime.utcnow().isoformat(),
            "updated": True
        }
        
        policy = self._get_encryption_policy(self.current_state)
        
        try:
            result = await self.container.replace_item(
                item=item["id"],
                body=item,
                encryption_policy=policy
            )
            self.logger.info(f"Updated encrypted item with id: {item['id']}")
            return result
        except Exception as e:
            self.logger.error(f"Error updating encrypted item: {str(e)}")
            raise

    async def delete_item(self, item_id: str, partition_key: Any):
        """Delete an encrypted item"""
        try:
            await self.container.delete_item(
                item=item_id,
                partition_key=partition_key
            )
            self.logger.info(f"Deleted encrypted item with id: {item_id}")
        except Exception as e:
            self.logger.error(f"Error deleting encrypted item: {str(e)}")
            raise

    async def update_encryption_state(self, new_state: YijingState):
        """Update the encryption state"""
        old_state = self.current_state
        self.current_state = new_state
        self.logger.info(f"State changed: {old_state.value} -> {new_state.value}")
        
        # Handle state-specific actions
        if new_state == YijingState.CHANGE:
            await self._rotate_keys()
        elif new_state == YijingState.DANGER:
            await self._enhance_security()
        elif new_state == YijingState.RETURN:
            await self._restore_normal()

    async def _rotate_keys(self):
        """Handle key rotation in CHANGE state"""
        self.logger.info("Initiating key rotation")
        # Key rotation is handled automatically by Azure Key Vault
        # We just need to update our encryption policy
        policy = self._get_encryption_policy(YijingState.CHANGE)
        self.container.default_encryption_policy = policy

    async def _enhance_security(self):
        """Handle enhanced security in DANGER state"""
        self.logger.info("Enhancing security measures")
        policy = self._get_encryption_policy(YijingState.DANGER)
        self.container.default_encryption_policy = policy

    async def _restore_normal(self):
        """Handle return to normal operations"""
        self.logger.info("Restoring normal security measures")
        policy = self._get_encryption_policy(YijingState.PEACE)
        self.container.default_encryption_policy = policy

# Example usage
async def main():
    # Initialize the encryption system
    encryption_system = YijingCosmosEncryption(
        cosmos_endpoint="your_cosmos_endpoint",
        key_vault_url="your_keyvault_url",
        database_id="your_database",
        container_id="your_container"
    )
    
    # Example document with different types of data
    document = {
        "id": "1",
        "sensitive": {
            "credentials": "secret123"
        },
        "baseline": {
            "user_info": "basic data"
        },
        "volatile": {
            "session_data": "temporary"
        },
        "critical": {
            "financial": "important data"
        }
    }
    
    # Create encrypted document
    encrypted_doc = await encryption_system.create_item(document)
    
    # Read and decrypt document
    decrypted_doc = await encryption_system.read_item(
        item_id="1",
        partition_key="1"
    )
    
    # Update encryption state for enhanced security
    await encryption_system.update_encryption_state(YijingState.DANGER)

if __name__ == "__main__":
    import asyncio
    asyncio.run(main())
```
