using UnityEngine;

public class InventoryView : MonoBehaviour
{
    internal IInventoryService inventory;

    private void Start()
    {
        Debug.Log($"Items: {inventory.ItemCount}");
    }
}
