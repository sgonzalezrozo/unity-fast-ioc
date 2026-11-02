using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FastIoC.API
{
    public class ConstrainToRootOnly : MonoBehaviour
    {
#if UNITY_EDITOR
        private void Reset()
        {
            bool isInsidePrefab = PrefabUtility.IsPartOfPrefabAsset(gameObject) ||
                                  PrefabStageUtility.GetCurrentPrefabStage() != null;

            if (!isInsidePrefab)
            {
                Debug.LogError(
                    $"[ConstrainToRootOnly] {gameObject.name} - This component can only be added to a Prefab asset, " +
                    $"not to a scene instance. Please add it only to prefabs in Assets/.",
                    gameObject);

                DestroyImmediate(this);
                return;
            }

            if (transform.parent != null)
            {
                Debug.LogError(
                    $"[ConstrainToRootOnly] {gameObject.name} - This component must be on the root GameObject of the prefab. " +
                    $"Child GameObjects are not allowed to have this component. " +
                    $"Current parent: {transform.parent.gameObject.name}",
                    gameObject);
                DestroyImmediate(this);
                return;
            }

            Debug.Log(
                $"[ConstrainToRootOnly] ✅ {gameObject.name} validated: Prefab root, ready for binding.",
                gameObject);
        }
#endif
    }
}

