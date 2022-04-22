using UnityEngine;

public class DummyScript : MonoBehaviour
{
    public KMAudio Audio;
    public KMBombInfo BombInfo;
    public KMBombModule Module;
    public KMSelectable Sel;

    private void Start()
    {
        Sel.OnInteract += delegate () { Module.HandlePass(); return false; };
    }
}