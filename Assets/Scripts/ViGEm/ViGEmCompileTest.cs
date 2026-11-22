using UnityEngine;
using Nefarius.ViGEm.Client;

public class ViGEmCompileTest : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("ViGEm compile test started.");

        ViGEmClient client = null;

        Debug.Log("ViGEmClient type is available: " + typeof(ViGEmClient).FullName);
    }
}