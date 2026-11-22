using UnityEngine;

// Rain World does not draw the oracle puppet head as a mesh. OracleGraphics builds
// it from three sprites and adds the side housings on top:
//
//   head   Circle20, scale (head.rad / 9, head.rad / 11), rotated along the
//          head-to-body axis
//   chin   Circle20, scale head.rad / 15, 15 % of the way from the head towards the
//          body, nudged sideways by the look direction
//   neck   pixel, scaleX 3 (Pebbles), scaleY = distance from the body to the head
//   eyes   pixel, scaleX 1 + look + (1 - eyesOpen), scaleY Lerp(1, 2, eyesOpen),
//          alpha 0.5 + 0.5 * eyesOpen
//   phone  Circle20 x2 (base and highlight) plus a LizardScaleA1 spike, mirrored on
//          both sides of the head
//
// Colors come from OracleGraphics.ApplyPalette: SkinColor() for head, chin and neck
// ((1, 0.4, 0.796) on Five Pebbles), white for the eyes, and the arm base and
// highlight colors for the phones.
[DefaultExecutionOrder(40)]
public sealed class PuppetHeadGraphics : MonoBehaviour
{
    [Header("Palette")]
    public Color skinColor = new Color(1f, 0.4f, 203f / 255f);
    public Color hardwareColor = new Color(0.335f, 0.318f, 0.288f);
    public Color hardwareHighlightColor = new Color(0.402f, 0.386f, 0.352f);
    public Color eyeColor = Color.white;

    [Header("Scale")]
    [Min(1f)] public float pixelsPerUnit = 100f;
    [Tooltip("Enlargement of the sprite layers, matching OracleArmSprites.detailScale.")]
    [Min(0.1f)] public float detailScale = 1.35f;
    [Tooltip("GenericBodyPart head radius used by OracleGraphics (5 px).")]
    [Min(1f)] public float headRadiusPx = 5f;

    [Header("Sorting orders")]
    public int neckOrder = -2;
    public int chinOrder = 3;
    public int headOrder = 2;
    public int eyeOrder = 4;
    public int phoneOrder = 5;

    private PuppetAnimator pose;
    private SSOraclePetBrain brain;
    private Sprite circleSprite;
    private Sprite pixelSprite;
    private Sprite spikeSprite;

    private SpriteRenderer headDisc;
    private SpriteRenderer chinDisc;
    private SpriteRenderer neckBar;
    private readonly SpriteRenderer[] phoneBase = new SpriteRenderer[2];
    private readonly SpriteRenderer[] phoneLip = new SpriteRenderer[2];
    private readonly SpriteRenderer[] phoneSpike = new SpriteRenderer[2];

    private bool ready;

    private void Awake()
    {
        pose = GetComponentInParent<PuppetAnimator>();
        if (pose == null || pose.head != transform || pose.body == null)
            return;

        brain = GetComponentInParent<SSOraclePetBrain>();

        // The legacy authors' meshes and hand placed sprites are replaced by the
        // original layers below.
        foreach (SpriteRenderer legacy in GetComponentsInChildren<SpriteRenderer>(true))
            legacy.enabled = false;

        circleSprite = Resources.Load<Sprite>("IteratorArm/Circle20");
        pixelSprite = Resources.Load<Sprite>("IteratorArm/pixel");
        spikeSprite = Resources.Load<Sprite>("IteratorArm/LizardScaleA1");
        if (circleSprite == null || pixelSprite == null || spikeSprite == null)
            return;

        // The head local scale used to describe the old mesh; the sprite layers carry
        // their own scale so the eyes can keep their plain pixel offsets.
        transform.localScale = Vector3.one;

        headDisc = CreateSprite("Head disc", circleSprite, skinColor, headOrder, transform);
        chinDisc = CreateSprite("Chin disc", circleSprite, skinColor, chinOrder, transform);
        neckBar = CreateSprite("Neck", pixelSprite, skinColor, neckOrder, transform);
        for (int side = 0; side < 2; side++)
        {
            phoneBase[side] = CreateSprite("Phone base " + side, circleSprite, hardwareColor, phoneOrder, transform);
            phoneLip[side] = CreateSprite("Phone lip " + side, circleSprite, hardwareHighlightColor, phoneOrder + 1, transform);
            phoneSpike[side] = CreateSprite("Phone spike " + side, spikeSprite, hardwareHighlightColor, phoneOrder + 1, transform);
        }

        PrepareEyes();
        ready = true;
    }

    private void PrepareEyes()
    {
        SpriteRenderer left = pose.eyeLeft != null ? pose.eyeLeft.GetComponent<SpriteRenderer>() : null;
        SpriteRenderer right = pose.eyeRight != null ? pose.eyeRight.GetComponent<SpriteRenderer>() : null;
        float eyeSize = 2f * detailScale;
        if (left != null)
        {
            left.enabled = true;
            left.sprite = pixelSprite;
            left.color = eyeColor;
            left.sortingOrder = eyeOrder;
            pose.eyeLeft.localScale = new Vector3(eyeSize, eyeSize, 1f);
            pose.eyeLeft.localPosition = new Vector3(-Pixel(2.5f), 0f, 0f);
        }
        if (right != null)
        {
            right.enabled = true;
            right.sprite = pixelSprite;
            right.color = eyeColor;
            right.sortingOrder = eyeOrder;
            pose.eyeRight.localScale = new Vector3(eyeSize, eyeSize, 1f);
            pose.eyeRight.localPosition = new Vector3(Pixel(2.5f), 0f, 0f);
        }
    }

    private void LateUpdate()
    {
        if (!ready || pose == null || pose.body == null)
            return;

        Vector3 headCenter = transform.position;
        // OracleGraphics draws the neck directly from body chunk 0 to the head.
        Vector2 bodyTop = pose.body.position;
        Vector2 headToBody = bodyTop - (Vector2)headCenter;
        if (headToBody.sqrMagnitude < 1e-8f)
            headToBody = Vector2.down;
        headToBody.Normalize();
        Vector2 across = new Vector2(-headToBody.y, headToBody.x);
        float headRotation = Aim(headToBody);
        Vector2 look = LookDirection((Vector2)headCenter);

        // Head: Circle20 with the original radius ratios, long axis across the neck.
        float headScaleX = headRadiusPx / 9f * detailScale;
        float headScaleY = headRadiusPx / 11f * detailScale;
        Place(headDisc, headCenter, headRotation, new Vector2(headScaleX, headScaleY));

        // Chin: 15 % of the way to the body plus a sideways look nudge.
        Vector2 chinCenter = Vector2.Lerp(headCenter, bodyTop, 0.15f) + across * (look.x * Pixel(2f));
        float chinScale = headRadiusPx / 15f * detailScale;
        Place(chinDisc, chinCenter, headRotation, new Vector2(chinScale, chinScale));

        // Neck: pixel bar from the body top to the head centre, 3 px wide on Pebbles.
        Vector2 neckDelta = (Vector2)headCenter - bodyTop;
        float neckLength = neckDelta.magnitude;
        if (neckLength > 0.0001f)
        {
            Place(neckBar, bodyTop + neckDelta * 0.5f, Aim(neckDelta / neckLength),
                new Vector2(3f * detailScale, neckLength * pixelsPerUnit));
        }

        // Phones: a disc pair plus a LizardScaleA1 spike on both sides of the head.
        float spread = Mathf.Lerp(7f, 5f, Mathf.Abs(look.x));
        float discWidth = Mathf.Lerp(3.5f, 5f, Mathf.Abs(look.x));
        for (int side = 0; side < 2; side++)
        {
            float sign = side == 0 ? -1f : 1f;
            Vector2 discCenter = (Vector2)headCenter + across * Pixel(Mathf.Clamp(spread * sign, -11f, 11f));
            Place(phoneBase[side], discCenter, headRotation,
                new Vector2(discWidth / 20f * detailScale, 5.5f / 20f * detailScale));
            Place(phoneLip[side], discCenter, headRotation,
                new Vector2(discWidth * 0.8f / 20f * detailScale, 5.5f * 0.8f / 20f * detailScale));

            // AimFromOneVectorToAnother(body, phone - down * 40 - look * 10) sends the
            // spike outward past the head, which is how the original side pieces read.
            Vector2 aimTarget = discCenter - headToBody * Pixel(40f) - look * Pixel(10f);
            Vector2 spikeDirection = aimTarget - bodyTop;
            if (spikeDirection.sqrMagnitude < 1e-8f)
                spikeDirection = -headToBody;
            spikeDirection.Normalize();
            float spikeScaleY = 0.8f * detailScale;
            float spikeLength = 14f * spikeScaleY / Mathf.Max(1f, pixelsPerUnit);
            Place(phoneSpike[side], discCenter + spikeDirection * (spikeLength * 0.5f),
                Aim(spikeDirection),
                new Vector2((side == 0 ? -0.75f : 0.75f) * detailScale, spikeScaleY));
        }
    }

    private Vector2 LookDirection(Vector2 headCenter)
    {
        if (brain == null)
            return Vector2.zero;
        Vector2 delta = (Vector2)brain.GazePoint - headCenter;
        if (delta.sqrMagnitude < 1e-6f)
            return Vector2.zero;
        return Vector2.ClampMagnitude(delta, 1f) * Mathf.Clamp01(brain.AttentionWeight);
    }

    private SpriteRenderer CreateSprite(string label, Sprite sprite, Color color, int order, Transform parent)
    {
        GameObject created = new GameObject(label);
        created.transform.SetParent(parent, false);
        SpriteRenderer renderer = created.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static void Place(SpriteRenderer renderer, Vector2 worldPosition, float zRotationDegrees, Vector2 scale)
    {
        Transform child = renderer.transform;
        child.position = new Vector3(worldPosition.x, worldPosition.y, 0f);
        child.localRotation = Quaternion.Euler(0f, 0f, zRotationDegrees);
        child.localScale = new Vector3(scale.x, scale.y, 1f);
    }

    private float Pixel(float value)
    {
        return value * detailScale / Mathf.Max(1f, pixelsPerUnit);
    }

    // Unity rotates counter clockwise; a sprite whose local +Y should follow
    // "direction" needs the angle minus 90 degrees. This matches Custom.VecToDeg.
    private static float Aim(Vector2 direction)
    {
        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
    }
}
