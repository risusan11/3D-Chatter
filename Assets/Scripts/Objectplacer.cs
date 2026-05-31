using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

/// <summary>
/// ゲームオブジェクト配置機能①（オブジェクトの召喚）— ローカル版
///
/// 【配置】
///   Avatar プレハブにアタッチする（DrawingPlacer / PointerUIController と同じ）。
///   playerCamera は Avatar の子カメラを自動取得する。
///
/// 【機能】
///   - 複数のプレハブをカタログで管理し、数字キーで選択切り替え
///   - マウスのレイキャスト着地点にゴーストプレビューを表示
///   - 左クリックで確定配置（召喚）
///   - ホイールで回転、Escでキャンセル
///
/// 【マルチ対応への布石】
///   召喚処理は SpawnObject() に集約。後でこの中身を
///   PhotonNetwork.Instantiate() に差し替えるだけで同期対応できる。
/// </summary>
[RequireComponent(typeof(PhotonView))]
public class ObjectPlacer : MonoBehaviourPun
{
    // ────────────────────────────────────────────
    // カタログ（配置可能なプレハブ一覧）
    // ────────────────────────────────────────────

    [System.Serializable]
    public class PlaceableEntry
    {
        public string displayName = "Object";

        [Tooltip("プレビュー表示用のプレハブ参照（ローカル表示のみに使用）")]
        public GameObject prefab;

        [Tooltip("Resources フォルダ内のプレハブ名（PhotonNetwork.Instantiate 用）。" +
                 "例：Resources/Chair.prefab なら \"Chair\"")]
        public string prefabName = "";

        [Tooltip("プレビュー/配置時の追加回転オフセット（Blender軸補正など）")]
        public Vector3 rotationOffset = Vector3.zero;
        [Tooltip("配置スケール")]
        public Vector3 scale = Vector3.one;
        [Tooltip("表面からの浮かせ量")]
        public float surfaceOffset = 0f;
    }

    [Header("📦 配置可能なオブジェクト一覧")]
    [SerializeField] private List<PlaceableEntry> catalog = new List<PlaceableEntry>();

    [Header("🎯 参照")]
    [Tooltip("空欄なら Avatar 配下のカメラ → Camera.main の順で自動取得")]
    [SerializeField] private Camera playerCamera;

    [Header("🔫 レイキャスト設定")]
    [SerializeField] private float maxRayDistance = 30f;
    [SerializeField] private LayerMask placementMask = ~0;

    [Header("👻 プレビュー外観")]
    [Tooltip("プレビュー時の半透明マテリアル（null なら自動生成）")]
    [SerializeField] private Material previewMaterial;
    [SerializeField] private Color previewValidColor   = new Color(0.3f, 1f, 0.4f, 0.5f);
    [SerializeField] private Color previewInvalidColor = new Color(1f, 0.3f, 0.3f, 0.5f);

    [Header("⌨️ 操作キー")]
    [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
    [Tooltip("ホイール1ノッチあたりの回転角度")]
    [SerializeField] private float rotateStep = 15f;

    [Header("🔧 モード切り替えキー")]
    [Tooltip("削除モードに入る/出るキー")]
    [SerializeField] private KeyCode deleteModeKey = KeyCode.H;
    [Tooltip("移動モードに入る/出るキー")]
    [SerializeField] private KeyCode moveModeKey   = KeyCode.J;

    [Header("🎨 削除モードのハイライト色")]
    [SerializeField] private Color deleteHighlightColor = Color.red;

    // ────────────────────────────────────────────
    // モード管理
    // ────────────────────────────────────────────

    public enum Mode { None, Place, Delete, Move }
    private Mode currentMode = Mode.None;
    public Mode CurrentMode => currentMode;

    // ────────────────────────────────────────────
    // 内部状態
    // ────────────────────────────────────────────

    private int        selectedIndex = 0;   // 現在選択中のカタログインデックス
    private GameObject previewInstance;      // ゴーストプレビュー
    private float      manualRotationY = 0f; // ホイールで追加した手動回転

    private Vector3    hitPoint;
    private Vector3    hitNormal;
    private bool       hasHit;

    // 削除モード：ホバー中のオブジェクト
    private PlacedObject hoveredObject = null;

    // 移動モード：掴んでいるオブジェクト
    private PlacedObject grabbedObject = null;

    public bool IsPlacing => currentMode == Mode.Place;

    // ────────────────────────────────────────────
    // ライフサイクル
    // ────────────────────────────────────────────

    void Start()
    {
        // ✅ マルチプレイ：自分のアバター以外では無効化（他人のObjectPlacerは動かさない）
        if (PhotonNetwork.IsConnected && !photonView.IsMine)
        {
            enabled = false;
            return;
        }

        // ✅ カメラ自動取得：Inspector未設定なら Avatar配下のカメラ → Camera.main の順で探す
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null) playerCamera = Camera.main;
            if (playerCamera == null)
                Debug.LogError("[ObjectPlacer] playerCamera が見つかりません。Avatar配下にカメラがあるか確認してください。");
        }
    }

    void Update()
    {
        // ✅ 自分のアバター以外では何もしない
        if (PhotonNetwork.IsConnected && !photonView.IsMine) return;

        // チャット中などは無効化したい場合はここでガード
        // if (RealisingMessageController.isChatting) return;

        HandleModeSwitchInput();

        switch (currentMode)
        {
            case Mode.None:
                break;
            case Mode.Place:
                UpdatePlaceMode();
                break;
            case Mode.Delete:
                UpdateDeleteMode();
                break;
            case Mode.Move:
                UpdateMoveMode();
                break;
        }
    }

    // ────────────────────────────────────────────
    // モード切り替え入力
    // ────────────────────────────────────────────

    private void HandleModeSwitchInput()
    {
        // 数字キー 1〜9 → そのカタログを選んで配置モードへ
        for (int i = 0; i < 9 && i < catalog.Count; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                SelectAndStartPlace(i);
                return;
            }
        }

        // 削除モードのトグル
        if (Input.GetKeyDown(deleteModeKey))
        {
            SwitchMode(currentMode == Mode.Delete ? Mode.None : Mode.Delete);
        }
        // 移動モードのトグル
        else if (Input.GetKeyDown(moveModeKey))
        {
            SwitchMode(currentMode == Mode.Move ? Mode.None : Mode.Move);
        }
        // キャンセル → 通常モードへ
        else if (Input.GetKeyDown(cancelKey))
        {
            SwitchMode(Mode.None);
        }
    }

    /// <summary>モードを切り替え、旧モードのクリーンアップを行う。</summary>
    private void SwitchMode(Mode next)
    {
        if (currentMode == next) return;

        // 旧モードの後始末
        switch (currentMode)
        {
            case Mode.Place:
                DestroyPreview();
                break;
            case Mode.Delete:
                ClearDeleteHover();
                break;
            case Mode.Move:
                grabbedObject = null;
                break;
        }

        currentMode = next;
        Debug.Log($"[ObjectPlacer] モード変更 ➡ {currentMode}");
    }

    // ════════════════════════════════════════════════
    //  配置モード（Place）
    // ════════════════════════════════════════════════

    private void UpdatePlaceMode()
    {
        UpdateRaycast();
        HandleRotationInput();
        UpdatePreviewTransform();

        if (Input.GetMouseButtonDown(0) && hasHit &&
            !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            ConfirmPlacement();
        }
    }

    // ────────────────────────────────────────────
    // カタログ選択 → 配置モード開始
    // ────────────────────────────────────────────

    /// <summary>カタログから選んで配置モードに入る。</summary>
    public void SelectAndStartPlace(int index)
    {
        if (index < 0 || index >= catalog.Count) return;
        if (catalog[index].prefab == null)
        {
            Debug.LogWarning($"[ObjectPlacer] index {index} のプレビュー用 prefab が未設定です。");
            return;
        }

        // 既存モードを抜けてから配置モードへ
        SwitchMode(Mode.None);

        selectedIndex   = index;
        manualRotationY = 0f;
        currentMode     = Mode.Place;

        BuildPreview();
        Debug.Log($"[ObjectPlacer] 配置モード開始 ➡ {catalog[index].displayName}");
    }

    // ────────────────────────────────────────────
    // レイキャスト
    // ────────────────────────────────────────────

    private void UpdateRaycast()
    {
        if (playerCamera == null) { hasHit = false; return; }

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, placementMask))
        {
            // プレビュー自身に当たらないようにする
            if (previewInstance != null && hit.collider.transform.IsChildOf(previewInstance.transform))
            {
                hasHit = false;
                return;
            }

            hitPoint  = hit.point;
            hitNormal = hit.normal;
            hasHit    = true;
        }
        else
        {
            hasHit = false;
        }
    }

    // ────────────────────────────────────────────
    // 回転入力
    // ────────────────────────────────────────────

    private void HandleRotationInput()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            manualRotationY += scroll > 0 ? rotateStep : -rotateStep;
        }
    }

    // ────────────────────────────────────────────
    // プレビュー
    // ────────────────────────────────────────────

    // ════════════════════════════════════════════════
    //  削除モード（Delete）— 消しゴム感覚でオブジェクトを狙って削除
    // ════════════════════════════════════════════════

    private void UpdateDeleteMode()
    {
        PlacedObject target = RaycastForPlacedObject();

        // ホバー対象が変わったらハイライトを更新
        if (target != hoveredObject)
        {
            if (hoveredObject != null) hoveredObject.SetHighlight(false, deleteHighlightColor);
            if (target != null)        target.SetHighlight(true, deleteHighlightColor);
            hoveredObject = target;
        }

        // クリックで削除
        if (Input.GetMouseButtonDown(0) && hoveredObject != null &&
            !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            PlacedObject toDelete = hoveredObject;
            hoveredObject = null;
            toDelete.RequestDelete();
            Debug.Log("[ObjectPlacer] オブジェクトを削除しました。");
        }
    }

    private void ClearDeleteHover()
    {
        if (hoveredObject != null) hoveredObject.SetHighlight(false, deleteHighlightColor);
        hoveredObject = null;
    }

    // ════════════════════════════════════════════════
    //  移動モード（Move）— クリックで掴む → 追従 → 再クリックで確定
    // ════════════════════════════════════════════════

    private void UpdateMoveMode()
    {
        if (grabbedObject == null)
        {
            // 掴んでいない：ホバーハイライト + クリックで掴む
            PlacedObject target = RaycastForPlacedObject();

            if (target != hoveredObject)
            {
                if (hoveredObject != null) hoveredObject.SetHighlight(false, deleteHighlightColor);
                if (target != null)        target.SetHighlight(true, deleteHighlightColor);
                hoveredObject = target;
            }

            if (Input.GetMouseButtonDown(0) && hoveredObject != null &&
                !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                grabbedObject = hoveredObject;
                grabbedObject.SetHighlight(false, deleteHighlightColor);
                grabbedObject.BeginMove(); // 所有権を取得
                hoveredObject = null;
            }
        }
        else
        {
            // 掴んでいる：マウス先の面に追従
            // ✅ RaycastAll で「掴んだオブジェクト自身」を除外してヒット点を取得
            //    （自分に当たると無限に手前へ寄ってくるバグを防ぐ）
            if (TryRaycastExcluding(grabbedObject, out Vector3 movePoint, out Vector3 moveNormal))
            {
                Quaternion rot = BuildSurfaceRotation(moveNormal);
                grabbedObject.UpdateMove(movePoint, rot);
            }

            // 再クリックで確定
            if (Input.GetMouseButtonDown(0) &&
                !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                Debug.Log("[ObjectPlacer] オブジェクトを移動確定しました。");
                grabbedObject = null;
            }
        }
    }

    /// <summary>
    /// 指定オブジェクトを除外してレイキャストし、最も近いヒット点を返す。
    /// 掴んでいるオブジェクト自身を無視するために使う。
    /// </summary>
    private bool TryRaycastExcluding(PlacedObject exclude, out Vector3 point, out Vector3 normal)
    {
        point  = Vector3.zero;
        normal = Vector3.up;
        if (playerCamera == null) return false;

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

        // 全ヒットを取得し、距離順にソートして「除外対象でない最初のヒット」を採用
        RaycastHit[] hits = Physics.RaycastAll(ray, maxRayDistance, placementMask);
        if (hits.Length == 0) return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            // 掴んでいるオブジェクト（とその子）に当たったヒットはスキップ
            if (exclude != null && hit.collider.GetComponentInParent<PlacedObject>() == exclude)
                continue;

            point  = hit.point;
            normal = hit.normal;
            return true;
        }
        return false;
    }

    // ────────────────────────────────────────────
    // レイキャストで PlacedObject を取得（削除・移動共通）
    // ────────────────────────────────────────────

    private PlacedObject RaycastForPlacedObject()
    {
        if (playerCamera == null) return null;

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, placementMask))
        {
            return hit.collider.GetComponentInParent<PlacedObject>();
        }
        return null;
    }

    private void BuildPreview()
    {
        DestroyPreview();

        PlaceableEntry entry = catalog[selectedIndex];
        previewInstance = Instantiate(entry.prefab);
        previewInstance.name = "__Preview__" + entry.displayName;

        // プレビューはコライダーを無効化（レイキャストを邪魔しない）
        foreach (var col in previewInstance.GetComponentsInChildren<Collider>())
            col.enabled = false;

        // 半透明マテリアルを適用
        ApplyPreviewMaterial(previewInstance);
    }

    private void UpdatePreviewTransform()
    {
        if (previewInstance == null) return;

        PlaceableEntry entry = catalog[selectedIndex];

        if (hasHit)
        {
            previewInstance.SetActive(true);

            // 表面の傾きに合わせた基本回転
            Quaternion surfaceRot = BuildSurfaceRotation(hitNormal);
            // 手動回転（ホイール）+ モデル補正回転を上乗せ
            Quaternion finalRot = surfaceRot
                                * Quaternion.Euler(0f, manualRotationY, 0f)
                                * Quaternion.Euler(entry.rotationOffset);

            previewInstance.transform.SetPositionAndRotation(
                hitPoint + hitNormal * entry.surfaceOffset, finalRot);
            previewInstance.transform.localScale = entry.scale;

            SetPreviewColor(previewValidColor);
        }
        else
        {
            // 配置不可（壁が見つからない）→ 赤くするか非表示
            SetPreviewColor(previewInvalidColor);
        }
    }

    private void DestroyPreview()
    {
        if (previewInstance != null) Destroy(previewInstance);
        previewInstance = null;
    }

    // ────────────────────────────────────────────
    // 配置確定（召喚）
    //
    // ✅ ここが「召喚」の中心。後で Photon 対応する際は
    //    Instantiate(...) を PhotonNetwork.Instantiate(...) に
    //    差し替えるだけでマルチ同期になる。
    // ────────────────────────────────────────────

    private void ConfirmPlacement()
    {
        PlaceableEntry entry = catalog[selectedIndex];

        Quaternion surfaceRot = BuildSurfaceRotation(hitNormal);
        Quaternion finalRot   = surfaceRot
                              * Quaternion.Euler(0f, manualRotationY, 0f)
                              * Quaternion.Euler(entry.rotationOffset);
        Vector3 spawnPos = hitPoint + hitNormal * entry.surfaceOffset;

        SpawnObject(entry, spawnPos, finalRot);

        Debug.Log($"[ObjectPlacer] 配置確定 ➡ {entry.displayName} @ {spawnPos}");

        // 連続配置したい場合はモードを維持。1回で終わるなら SwitchMode(Mode.None) を呼ぶ。
        // SwitchMode(Mode.None);
    }

    /// <summary>
    /// 実際の召喚処理。PhotonNetwork.Instantiate で全クライアントに同期生成する。
    ///
    /// ✅ 注意点：
    ///   - entry.prefabName は Resources フォルダ内のプレハブ名と一致させること
    ///   - プレハブには PhotonView が必須
    ///   - スケールは InstantiationData 経由で全クライアントに渡す
    ///     （PhotonNetwork.Instantiate は position/rotation は同期するが scale は同期しないため）
    /// </summary>
    private void SpawnObject(PlaceableEntry entry, Vector3 position, Quaternion rotation)
    {
        if (string.IsNullOrEmpty(entry.prefabName))
        {
            Debug.LogError($"[ObjectPlacer] '{entry.displayName}' の prefabName が未設定です。" +
                           "Resources 内のプレハブ名を入力してください。");
            return;
        }

        // スケールを InstantiationData として渡す（生成側で適用）
        object[] data = new object[] { entry.scale.x, entry.scale.y, entry.scale.z };

        PhotonNetwork.Instantiate(entry.prefabName, position, rotation, 0, data);
    }

    // ────────────────────────────────────────────
    // ユーティリティ
    // ────────────────────────────────────────────

    private Quaternion BuildSurfaceRotation(Vector3 normal)
    {
        Vector3 worldRef = (Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.99f)
            ? Vector3.forward : Vector3.up;
        Vector3 right   = Vector3.Cross(worldRef, normal).normalized;
        Vector3 forward = Vector3.Cross(normal, right).normalized;
        return Quaternion.LookRotation(forward, normal);
    }

    // ── プレビュー用マテリアル処理 ──

    private List<Renderer> previewRenderers = new List<Renderer>();

    private void ApplyPreviewMaterial(GameObject target)
    {
        previewRenderers.Clear();

        Material previewMat = previewMaterial != null
            ? new Material(previewMaterial)
            : CreateDefaultPreviewMaterial();

        foreach (var rend in target.GetComponentsInChildren<Renderer>())
        {
            // 元のマテリアル数に合わせて半透明マテリアルを割り当て
            Material[] mats = new Material[rend.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = previewMat;
            rend.materials = mats;

            previewRenderers.Add(rend);
        }
    }

    private void SetPreviewColor(Color color)
    {
        foreach (var rend in previewRenderers)
        {
            if (rend == null) continue;
            foreach (var mat in rend.materials)
                if (mat != null) mat.color = color;
        }
    }

    private Material CreateDefaultPreviewMaterial()
    {
        // 半透明レンダリング可能な簡易マテリアル
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = previewValidColor;
        return mat;
    }

    void OnDestroy()
    {
        DestroyPreview();
    }
}