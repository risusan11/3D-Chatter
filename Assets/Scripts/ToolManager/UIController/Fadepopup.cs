 using System.Collections;
using UnityEngine;
using UnityEngine.UI;


[RequireComponent(typeof(CanvasGroup))]
public class FadePopup : MonoBehaviour
{
   
    [SerializeField] private Text messageText;

    [SerializeField] private float fadeInDuration = 0.3f;  // ふわっと出る時間
    [SerializeField] private float holdDuration = 3f;      // 表示を維持する時間
    [SerializeField] private float fadeOutDuration = 0.4f; // 消える時間


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

    public void Show(string message = null, float? hold = null)
    {
        if (!string.IsNullOrEmpty(message) && messageText != null)
            messageText.text = message;

        if (running != null) StopCoroutine(running);
        running = StartCoroutine(PlayRoutine(hold ?? holdDuration));
    }

    private IEnumerator PlayRoutine(float hold)
    {
        Vector2 startPos = basePos - new Vector2(0f, riseDistance);

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

        yield return new WaitForSeconds(hold);

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