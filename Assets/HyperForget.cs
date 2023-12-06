using KModkit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TheHypercube;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class HyperForget : MonoBehaviour
{
    public KMBombInfo BombInfo;
    public KMBombModule Module;
    public KMBossModule Boss;
    public KMAudio Audio;
    public Transform[] Edges;
    public KMSelectable[] Vertices;
    public MeshFilter Faces;
    public Mesh Quad;
    public TextMesh StageText;
    public GameObject HypercubeParent;

    private int moduleId;
    private static int moduleIdCounter = 1;
    private bool moduleSolved;

    private float hue = 0.5f;
    private float sat = 0f;
    private float v = 0.5f;
    private Coroutine rotationCoroutine;

    private Material edgesMat, verticesMat, facesMat;
    private Mesh lastFacesMesh = null;
    private string serialNumber;
    private static readonly string[] rotationNames = new string[] { "XY", "XZ", "XW", "YX", "YZ", "YW", "ZX", "ZY", "ZW", "WX", "WY", "WZ" };
    private static readonly string[] rotationTable = new string[] { "+.+.", "+..-", "..+-", "++..", "-.+.", "+-..", ".-.+", ".-.-", "..++", "+..+", ".+-.", ".++." };

    private string[] ignoredModules;
    private int stageCount;
    private int currentStage = -1;
    private int currentSolves;
    private List<string> rotationList = new List<string>();
    private List<string> correctVertices = new List<string>();

    private bool readyToAdvance;
    private bool stageRecovery;
    private bool submissionMode;

    private bool allowedToPress;
    private bool canRotate = true;
    private bool shouldChangeColor;
    private bool isAnimating;
    private bool canContinue = true;
    private bool shouldStopRotating;
    private int currentSubmission;

    private void Start()
    {
        moduleId = moduleIdCounter++;
        serialNumber = BombInfo.GetSerialNumber();
        edgesMat = Edges[0].GetComponent<MeshRenderer>().material;
        for (int i = 0; i < Edges.Length; i++)
            Edges[i].GetComponent<MeshRenderer>().sharedMaterial = edgesMat;
        verticesMat = Vertices[0].GetComponent<MeshRenderer>().material;
        for (int i = 0; i < Vertices.Length; i++)
            Vertices[i].GetComponent<MeshRenderer>().sharedMaterial = verticesMat;
        facesMat = Faces.GetComponent<MeshRenderer>().material;
        SetHypercube(GetUnrotatedVertices().Select(p => p.Project()).ToArray());
        StartCoroutine(Init());
        for (var i = 0; i < 1 << 4; i++)
            Vertices[i].OnInteract = VertexClick(i);
    }

    private IEnumerator Init()
    {
        yield return null;
        SetColor(hue, sat, v);
        if (ignoredModules == null)
            ignoredModules = Boss.GetIgnoredModules("HyperForget", new string[] { "HyperForget" });
        stageCount = BombInfo.GetSolvableModuleNames().Count(a => !ignoredModules.Contains(a));
        if (stageCount == 0)
        {
            Module.HandlePass();
            Debug.LogFormat("[HyperForget #{0}] Zero stages were generated. Solving module.", moduleId);
            yield break;
        }
        readyToAdvance = true;
        for (int i = 0; i < stageCount; i++)
        {
            int rand = Rnd.Range(0, rotationNames.Length);
            rotationList.Add(rotationNames[rand]);
            int val = (serialNumber[i % 6] >= '0' && serialNumber[i % 6] <= '9' ? serialNumber[i % 6] - '0' : (serialNumber[i % 6] - 'A' + 1)) % 4;
            int ix = 0;
            string str = "";
            for (int j = 0; j < 4; j++)
            {
                if (rotationTable[rand][j] != '.')
                    str += rotationTable[rand][j];
                else
                {
                    if (ix == 0) { if (val / 2 == 0) str += "-"; else str += "+"; ix = 1; }
                    else { if (val % 2 == 0) str += "-"; else str += "+"; ix = 0; }
                }
            }
            correctVertices.Add(str);
            Debug.LogFormat("[HyperForget #{0}] Stage {1}: Rotation is {2}. Correct vertex is {3}.", moduleId, i + 1, rotationList[i], correctVertices[i]);
        }
        rotationCoroutine = StartCoroutine(RotateHypercube());
    }
    private KMSelectable.OnInteractHandler VertexClick(int v)
    {
        return delegate
        {
            Vertices[v].AddInteractionPunch(.2f);
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
            if (moduleSolved || !allowedToPress || isAnimating)
                return false;
            if (stageRecovery)
            {
                shouldStopRotating = true;
                shouldChangeColor = true;
                canContinue = true;
                return false;
            }
            if (submissionMode)
            {
                string vtx = GetVertexString(v);
                if (vtx == correctVertices[currentSubmission])
                {
                    Debug.LogFormat("[HyperForget #{0}] Correctly pressed {1} at Stage {2}.", moduleId, vtx, currentSubmission + 1);
                    currentSubmission++;
                    StageText.text = ((currentSubmission + 1) % 100).ToString("000");
                    if (currentSubmission == stageCount)
                    {
                        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
                        moduleSolved = true;
                        Module.HandlePass();
                        Debug.LogFormat("[HyperForget #{0}] Module solved.", moduleId);
                        StageText.text = "GG";
                        StartCoroutine(ShrinkHypercube());
                        return false;
                    }
                }
                else
                {
                    Module.HandleStrike();
                    Debug.LogFormat("[HyperForget #{0}] Pressed {1} at Stage {2}, when {3} was expected. Strike.", moduleId, vtx, currentSubmission + 1, correctVertices[currentSubmission]);
                    stageRecovery = true;
                    canContinue = false;
                    canRotate = true;
                    rotationCoroutine = StartCoroutine(RotateHypercube());
                }
            }
            return false;
        };
    }

    private void Update()
    {
        if (!readyToAdvance || stageRecovery)
            return;
        currentSolves = BombInfo.GetSolvedModuleNames().Count(a => !ignoredModules.Contains(a));
        if (currentStage == currentSolves || submissionMode)
            return;
        if (currentStage <= stageCount)
            DoNextStage();
    }

    private void DoNextStage()
    {
        currentStage++;
        shouldChangeColor = true;
        PlayRandomSound();
        if (currentStage != stageCount)
            StageText.text = ((currentStage + 1) % 100).ToString("000");
        else
        {
            currentSubmission = 0;
            submissionMode = true;
            StageText.text = ((currentSubmission + 1) % 100).ToString("000");
            if (rotationCoroutine != null)
                StopCoroutine(rotationCoroutine);
            ChangeToWhite();
        }
    }

    private Point4D[] GetUnrotatedVertices()
    {
        return Enumerable.Range(0, 1 << 4).Select(i => new Point4D((i & 1) != 0 ? 1 : -1, (i & 2) != 0 ? 1 : -1, (i & 4) != 0 ? 1 : -1, (i & 8) != 0 ? 1 : -1)).ToArray();
    }

    private IEnumerator ColorChange(bool white = false)
    {
        isAnimating = true;
        for (int i = 0; i < Vertices.Length; i++)
            Vertices[i].GetComponent<MeshRenderer>().sharedMaterial = verticesMat;
        var prevHue = hue;
        var prevSat = sat;
        var prevV = v;
        if (white) { hue = 0.6f; sat = 0.1f; v = 1f; }
        else
        {
            newColor:
            hue = Rnd.Range(0f, 1f);
            sat = Rnd.Range(.6f, .9f);
            v = Rnd.Range(.75f, 1f);
            if (Math.Abs(hue - prevHue) < 0.1f || Math.Abs(hue - prevHue) > 0.5f)
                goto newColor;
        }
        var duration = 1f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            SetColor(Mathf.Lerp(prevHue, hue, elapsed / duration), Mathf.Lerp(prevSat, sat, elapsed / duration), Mathf.Lerp(prevV, v, elapsed / duration));
            yield return null;
            elapsed += Time.deltaTime;
        }
        SetColor(hue, sat, v);
        isAnimating = false;
        if (submissionMode && canContinue)
        {
            allowedToPress = true;
            canRotate = false;
            submissionMode = true;
            if (stageRecovery && white)
                stageRecovery = false;
            yield return null;
        }
    }

    private void ChangeToWhite()
    {
        if (stageRecovery)
            PlayRandomSound();
        StartCoroutine(ColorChange(true));
    }

    private void PlayRandomSound()
    {
        Audio.PlaySoundAtTransform("Bleep" + Rnd.Range(1, 11), transform);
    }

    private void SetColor(float h, float s, float v)
    {
        edgesMat.color = Color.HSVToRGB(h, s, v);
        verticesMat.color = Color.HSVToRGB(h, s * .8f, v * .5f);
        var clr = Color.HSVToRGB(h, s * .8f, v * .75f);
        clr.a = .1f;
        facesMat.color = clr;
    }

    private IEnumerator RotateHypercube()
    {
        yield return null;
        readyToAdvance = false;
        var unrotatedVertices = GetUnrotatedVertices();
        SetHypercube(unrotatedVertices.Select(v => v.Project()).ToArray());
        while (canRotate)
        {
            var axis1 = "XYZW".IndexOf(rotationList[stageRecovery ? currentSubmission : currentStage][0]);
            var axis2 = "XYZW".IndexOf(rotationList[stageRecovery ? currentSubmission : currentStage][1]);
            readyToAdvance = false;
            if (shouldChangeColor)
            {
                shouldChangeColor = false;
                if (!stageRecovery && submissionMode)
                    ChangeToWhite();
                else
                {
                    readyToAdvance = false;
                    StartCoroutine(ColorChange());
                }
                while (isAnimating)
                    yield return null;
            }
            if (stageRecovery)
                allowedToPress = true;
            yield return new WaitForSeconds(0.75f);
            var duration = 2f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                var angle = Easing.InOutQuad(elapsed, 0, Mathf.PI / 2, duration);
                var matrix = new double[16];
                for (int i = 0; i < 4; i++)
                    for (int j = 0; j < 4; j++)
                        matrix[i + 4 * j] =
                            i == axis1 && j == axis1 ? Mathf.Cos(angle) :
                            i == axis1 && j == axis2 ? Mathf.Sin(angle) :
                            i == axis2 && j == axis1 ? -Mathf.Sin(angle) :
                            i == axis2 && j == axis2 ? Mathf.Cos(angle) :
                            i == j ? 1 : 0;
                SetHypercube(unrotatedVertices.Select(v => (v * matrix).Project()).ToArray());
                yield return null;
                elapsed += Time.deltaTime;
            }
            SetHypercube(unrotatedVertices.Select(v => v.Project()).ToArray());
            yield return new WaitForSeconds(0.5f);
            readyToAdvance = true;
            yield return null;
            if (shouldStopRotating)
            {
                canRotate = false;
                shouldStopRotating = false;
            }
        }
        rotationCoroutine = null;
        if (submissionMode && stageRecovery)
            ChangeToWhite();
    }

    private void SetHypercube(Vector3[] vertices)
    {
        for (int i = 0; i < 1 << 4; i++)
            Vertices[i].transform.localPosition = vertices[i];
        var e = 0;
        for (int i = 0; i < 1 << 4; i++)
            for (int j = i + 1; j < 1 << 4; j++)
                if (((i ^ j) & ((i ^ j) - 1)) == 0)
                {
                    Edges[e].localPosition = (vertices[i] + vertices[j]) / 2;
                    Edges[e].localRotation = Quaternion.FromToRotation(Vector3.up, vertices[j] - vertices[i]);
                    Edges[e].localScale = new Vector3(.1f, (vertices[j] - vertices[i]).magnitude / 2, .1f);
                    e++;
                }
        if (lastFacesMesh != null)
            Destroy(lastFacesMesh);
        var f = 0;
        var triangles = new List<int>();
        for (int i = 0; i < 1 << 4; i++)
            for (int j = i + 1; j < 1 << 4; j++)
            {
                var b1 = i ^ j;
                var b2 = b1 & (b1 - 1);
                if (b2 != 0 && (b2 & (b2 - 1)) == 0 && (i & b1 & ((i & b1) - 1)) == 0 && (j & b1 & ((j & b1) - 1)) == 0)
                {
                    triangles.AddRange(new[] { i, i | j, i & j, i | j, i & j, j, i & j, i | j, i, j, i & j, i | j });
                    f++;
                }
            }
        lastFacesMesh = new Mesh { vertices = vertices, triangles = triangles.ToArray() };
        lastFacesMesh.RecalculateNormals();
        Faces.sharedMesh = lastFacesMesh;
    }

    private string GetVertexString(int num)
    {
        string s = "";
        s += num / 1 % 2 == 1 ? "+" : "-";
        s += num / 2 % 2 == 1 ? "+" : "-";
        s += num / 4 % 2 == 1 ? "+" : "-";
        s += num / 8 % 2 == 1 ? "+" : "-";
        return s;
    }

    private int GetVertexNum(string str)
    {
        int val = 0;
        if (str[3] == '+')
            val += 8;
        if (str[2] == '+')
            val += 4;
        if (str[1] == '+')
            val += 2;
        if (str[0] == '+')
            val += 1;
        return val;
    }

    private IEnumerator ShrinkHypercube()
    {
        var duration = 1f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            HypercubeParent.transform.localScale = new Vector3(Easing.InQuad(elapsed, 0.03f, 0f, duration), Easing.InQuad(elapsed, 0.03f, 0f, duration), Easing.InQuad(elapsed, 0.03f, 0f, duration));
            yield return null;
            elapsed += Time.deltaTime;
        }
        HypercubeParent.transform.localScale = new Vector3(0, 0, 0);
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "!{0} press ++-+ -+-+ +-+- [Press the vertices at those positions. Order of axes is X, Y, Z, W.]";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        if (!submissionMode)
        {
            yield return "sendtochaterror It is not yet time to submit! Command ignored.";
            yield break;
        }
        var parameters = command.ToLowerInvariant().Split(' ');
        int ix = (parameters[0] == "press" || parameters[0] == "submit") ? 1 : 0;
        var list = new List<int>();
        for (int i = ix; i < parameters.Length; i++)
        {
            if (parameters[i].Length != 4)
                yield break;
            for (int j = 0; j < 4; j++)
            {
                if (parameters[i][j] != '-' && parameters[i][j] != '+')
                    yield break;
            }
            list.Add(GetVertexNum(parameters[i]));
        }
        yield return null;
        while (moduleSolved || !allowedToPress || isAnimating)
            yield return null;
        if (stageRecovery)
        {
            Vertices[0].OnInteract();
            while (stageRecovery)
                yield return null;
        }
        for (int i = 0; i < list.Count; i++)
        {
            Vertices[list[i]].OnInteract();
            yield return new WaitForSeconds(0.2f);
        }
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        while (moduleSolved || !allowedToPress || isAnimating)
            yield return true;
        if (stageRecovery)
        {
            Vertices[0].OnInteract();
            while (stageRecovery)
                yield return true;
        }
        while (!moduleSolved)
        {
            Vertices[GetVertexNum(correctVertices[currentSubmission])].OnInteract();
            yield return new WaitForSeconds(0.2f);
        }
        yield break;
    }
}