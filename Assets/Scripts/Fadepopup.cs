 using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 🎯 汎用フェードポップアップ
///
/// Show() を呼ぶと「ふわっとフェードイン＋少し上昇 → 一定時間キープ → フェードアウト」で
/// メッセージを表示して自動で消える。名前復元の「おかえり」通知などに使う。
///
/// 📝 使い方：
///   1. Canvas配下にポップアップ用のオブジェクト（Image＋Text など）を作る
///   2. そのルートに CanvasGroup と この FadePopup を付ける
///   3. messageText に表示用のTextを割り当てる（固定文言ならInspectorで設定でもOK）
///   4. 他スクリプトから popup.Show("メッセージ") で呼ぶ
///
/// CanvasGroup を使うのは、alpha 1本で子要素まとめて透明度を制御でき、
/// クリック透過(blocksRaycasts)も扱いやすいため。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class FadePopup : MonoBehaviour
{
    [Header("表示")]
    [Tooltip("メッセージを表示するText（任意。固定文言ならInspectorのTextに直接書いてもよい）")]
    [SerializeField] private Text messageText;

    [Header("タイミング（秒）")]
    [SerializeField] private float fadeInDuration = 0.3f;  // ふわっと出る時間
    [SerializeField] private float holdDuration = 3f;      // 表示を維持する時間
    [SerializeField] private float fadeOutDuration = 0.4f; // 消える時間

    [Header("動き")]
    [Tooltip("下から何ピクセル上に昇りながら出るか（ふわっと感）")]
    [SerializeField] private float riseDistance = 20f;

    private CanvasGroup canvasGroup;
    private RectTransform rect;
    private Vector2 basePos;        // 本来の表示位置
    private Coroutine running;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        rect = GetComponent<RectTransform>();
        basePos = rect.anchoredPosition;

        // 最初は隠しておく
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false; // 後ろのボタン等を邪魔しない
    }

    /// <summary>ポップアップを表示する。messageを渡せばText内容を差し替える。</summary>
    public void Show(string message = null, float? hold = null)
    {
        if (!string.IsNullOrEmpty(message) && messageText != null)
            messageText.text = message;

        // 連続で呼ばれたら前の演出を止めて出し直す
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(PlayRoutine(hold ?? holdDuration));
    }

    private IEnumerator PlayRoutine(float hold)
    {
        Vector2 startPos = basePos - new Vector2(0f, riseDistance);

        // ① フェードイン＋上昇
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / fadeInDuration); // ぬるっと
            canvasGroup.alpha = p;
            rect.anchoredPosition = Vector2.Lerp(startPos, basePos, p);
            yield return null;
        }
        canvasGroup.alpha = 1f;
        rect.anchoredPosition = basePos;

        // ② 表示を維持
        yield return new WaitForSeconds(hold);

        // ③ フェードアウト
        t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / fadeOutDuration);
            canvasGroup.alpha = 1f - p;
            yield return null;
        }
        canvasGroup.alpha = 0f;
        rect.anchoredPosition = basePos; // 次回に備えて位置を戻す
        running = null;
    }
}