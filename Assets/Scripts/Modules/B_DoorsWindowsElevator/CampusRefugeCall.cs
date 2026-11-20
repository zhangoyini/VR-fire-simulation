using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class CampusRefugeCall : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform trackedHead;
    [SerializeField] private Collider refugeVolume;
    [SerializeField] private ResolutionCoordinator resolution;
    private Transform cap;
    private Mesh symbolMesh;
    private Material housingMaterial, capMaterial;

    private void Start()
    {
        var bell = FireTrainingUIStyle.Load("icons/icon_alarm_bell");
        var symbolMaterial = Resources.Load<Material>("FireTrainingUI/RefugeCallSymbol");
        if (bell == null || symbolMaterial == null || !TryGetComponent<MeshRenderer>(out var housing) ||
            !TryGetComponent<MeshFilter>(out var housingMesh)) return;

        housingMaterial = new Material(housing.sharedMaterial) { color = new Color(.10f, .11f, .12f) };
        housing.sharedMaterial = housingMaterial;
        capMaterial = new Material(housingMaterial) { color = new Color(.20f, .21f, .23f) };
        cap = new GameObject("Refuge call button cap", typeof(MeshFilter), typeof(MeshRenderer)).transform;
        cap.SetParent(transform, false);
        cap.localPosition = Vector3.back * .53f;
        cap.localScale = new Vector3(.94f, .91f, .22f);
        cap.GetComponent<MeshFilter>().sharedMesh = housingMesh.sharedMesh;
        cap.GetComponent<MeshRenderer>().sharedMaterial = capMaterial;

        // The symbol sits on the moving face and uses the same lighting as the housing.
        var symbol = new GameObject("Refuge call symbol", typeof(MeshFilter), typeof(MeshRenderer));
        symbol.transform.SetParent(cap, false);
        symbol.transform.localPosition = Vector3.back * .54f;
        symbolMesh = CreateSymbolMesh(bell, cap.lossyScale);
        symbol.GetComponent<MeshFilter>().sharedMesh = symbolMesh;
        var symbolRenderer = symbol.GetComponent<MeshRenderer>();
        symbolRenderer.sharedMaterial = symbolMaterial;
        symbolRenderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    private static Mesh CreateSymbolMesh(Sprite sprite, Vector3 capSize)
    {
        Rect pixels = sprite.rect;
        Vector2 uvMin = new Vector2(pixels.xMin / sprite.texture.width, pixels.yMin / sprite.texture.height);
        Vector2 uvMax = new Vector2(pixels.xMax / sprite.texture.width, pixels.yMax / sprite.texture.height);
        float aspect = pixels.width / pixels.height;
        float width = Mathf.Min(capSize.x, capSize.y * aspect) * .76f;
        float x = width / (2f * capSize.x);
        float y = width / (2f * capSize.y * aspect);
        var mesh = new Mesh
        {
            name = "Refuge call bell face",
            vertices = new[] { new Vector3(-x, -y), new Vector3(-x, y), new Vector3(x, y), new Vector3(x, -y) },
            uv = new[] { uvMin, new Vector2(uvMin.x, uvMax.y), uvMax, new Vector2(uvMax.x, uvMin.y) },
            triangles = new[] { 0, 1, 2, 0, 2, 3 }
        };
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        return mesh;
    }

    public void OnHoverEnter() { }
    public void OnHoverExit() { }
    public void OnInteract(Transform hand) { }
    public void OnInteractEnd(Transform hand) { }
    public void OnInteractStart(Transform hand)
    {
        var lesson = CampusLessonDirector.Instance;
        if (lesson == null || !lesson.ExerciseStarted || Time.timeScale == 0) return;
        if (trackedHead == null || refugeVolume == null || resolution == null ||
            resolution.CurrentOutcome != ResolutionCoordinator.Outcome.None) return;
        if (!refugeVolume.bounds.Contains(trackedHead.position)) return;
        if (lesson.ConfirmRescue() && cap != null) StartCoroutine(AnimatePress());
    }

    private IEnumerator AnimatePress()
    {
        Vector3 raised = cap.localPosition;
        const float duration = .20f;
        for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            cap.localPosition = raised + Vector3.forward * (.13f * Mathf.Sin(elapsed / duration * Mathf.PI));
            yield return null;
        }
        cap.localPosition = raised;
    }

    private void OnDestroy()
    {
        Release(housingMaterial);
        Release(capMaterial);
        Release(symbolMesh);
    }

    private static void Release(Object instance)
    {
        if (instance == null) return;
        if (Application.isPlaying) Destroy(instance);
        else DestroyImmediate(instance);
    }
}
