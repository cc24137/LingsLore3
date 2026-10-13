using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class LingAnimatorControllerTests
{
    private static readonly string[] States =
    {
        "Idle", "Walk", "Sprint", "Jump", "Fall", "Death", "Dodge",
        "ChargeUp", "ChargeDown", "Light1", "Light2", "Light3",
        "LingHeavyUp", "LingHeavyDownAttack", "AirSpin"
    };

    private GameObject visual;
    private Animator animator;
    private Transform fork;

    [SetUp]
    public void SetUp()
    {
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Animations/LingVisual.controller");
        Assert.That(controller, Is.Not.Null);

        visual = new GameObject("LingVisual");
        visual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        var pivot = new GameObject("PivotGarfo").transform;
        pivot.SetParent(visual.transform, false);
        fork = new GameObject("Garfo").transform;
        fork.SetParent(pivot, false);
        fork.localPosition = new Vector3(-0.1f, 0.6f, 0.6f);
        fork.localRotation = Quaternion.Euler(-75f, 0f, 0f);
        new GameObject("ModeloLing").transform.SetParent(visual.transform, false);

        animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.applyRootMotion = false;
        animator.Rebind();
        SelectState("Idle");
        animator.Update(0f);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(visual);
    }

    // Em Any State, uma transicao pode interromper a si mesma durante o blend,
    // mesmo com Can Transition To Self desmarcado. Testa o controller real.
    [TestCase(30)]
    [TestCase(60)]
    [TestCase(120)]
    public void AllStateChangesFinishTheirTransition(int fps)
    {
        foreach (string source in States)
        foreach (string destination in States)
        {
            SelectState(source);
            Advance(0.15f, fps);
            SelectState(destination);
            Advance(0.15f, fps);
            AssertState(destination, source + " -> " + destination);
        }
    }

    [TestCase("Idle")]
    [TestCase("Walk")]
    [TestCase("Sprint")]
    public void LocomotionAdvancesAndLoopsWithoutRestarting(string state)
    {
        SelectState(state);
        Advance(0.15f, 60);
        AssertState(state);
        float startTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        float startHeight = visual.transform.localPosition.y;
        Advance(0.2f, 60);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime,
            Is.GreaterThan(startTime));
        Assert.That(Mathf.Abs(visual.transform.localPosition.y - startHeight),
            Is.GreaterThan(0.001f), "O tempo e a pose devem avancar.");
        Advance(7f, 60);
        AssertState(state);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime,
            Is.GreaterThan(1f));
    }

    [TestCase(30)]
    [TestCase(60)]
    [TestCase(120)]
    public void LightAttackReturnsToIdleAndCanPlayAgain(int fps)
    {
        for (int repeat = 0; repeat < 3; repeat++)
        {
            SelectState("Light1");
            Advance(0.3f, fps);
            AssertState("Light1");
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime,
                Is.GreaterThan(0.5f));
            Assert.That(fork.localPosition.x, Is.LessThan(-0.5f));
            Advance(0.15f, fps);
            SelectState("Idle");
            Advance(0.15f, fps);
            AssertState("Idle");
            Assert.That(Vector3.Distance(fork.localPosition, new Vector3(-0.1f, 0.6f, 0.6f)),
                Is.LessThan(0.001f), "A pose do ataque nao deve ficar presa no blend de saida.");
        }
    }

    [TestCase("Idle", 0.1f)]
    [TestCase("Walk", 0.05f)]
    [TestCase("Sprint", 0.05f)]
    public void LocomotionKeepsVisualHeightAndBobbing(string state, float amplitude)
    {
        const float standingHeight = 0.5f;
        SelectState(state);
        Advance(0.15f, 60);
        float minHeight = float.PositiveInfinity;
        float maxHeight = float.NegativeInfinity;
        for (int frame = 0; frame < 420; frame++)
        {
            animator.Update(1f / 60f);
            float height = visual.transform.localPosition.y;
            minHeight = Mathf.Min(minHeight, height);
            maxHeight = Mathf.Max(maxHeight, height);
        }

        Assert.That(minHeight, Is.EqualTo(standingHeight).Within(0.001f),
            "O clip deve preservar a altura de apoio do LingVisual.");
        Assert.That(maxHeight - minHeight, Is.EqualTo(amplitude).Within(0.001f),
            "O balanco vertical original deve ser preservado.");
    }

    [TestCase("Death")]
    [TestCase("Dodge")]
    [TestCase("Light1")]
    public void ActionsCanInterruptALocomotionBlend(string action)
    {
        SelectState("Walk");
        animator.Update(1f / 120f);
        Assert.That(animator.IsInTransition(0), Is.True);
        SelectState(action);
        animator.Update(1f / 120f);
        AssertState(action, "A acao deve ter prioridade sobre o blend de locomocao.");
    }

    private void AssertState(string state, string message = null)
    {
        Assert.That(animator.IsInTransition(0), Is.False, message);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + state),
            Is.True, message ?? state);
    }

    private void Advance(float seconds, int fps)
    {
        for (int frame = 0; frame < Mathf.CeilToInt(seconds * fps); frame++)
        {
            animator.Update(1f / fps);
            Assert.That(visual.transform.localPosition.y, Is.GreaterThanOrEqualTo(0.4999f),
                "O visual nao deve afundar durante a reproducao ou as transicoes.");
        }
    }

    private void SelectState(string state)
    {
        bool locomotion = state == "Idle" || state == "Walk" || state == "Sprint" ||
            state == "Jump" || state == "Fall";
        int attackId = state == "Light1" ? 1 : state == "Light2" ? 2 :
            state == "Light3" ? 3 : state == "LingHeavyUp" ? 4 :
            state == "LingHeavyDownAttack" ? 5 : state == "AirSpin" ? 6 : 0;
        animator.SetBool("Free", locomotion);
        animator.SetBool("Grounded", state != "Jump" && state != "Fall" && state != "AirSpin");
        animator.SetBool("Moving", state == "Walk" || state == "Sprint");
        animator.SetBool("Sprinting", state == "Sprint");
        animator.SetBool("Rising", state == "Jump");
        animator.SetBool("Dead", state == "Death");
        animator.SetBool("Dodging", state == "Dodge");
        animator.SetBool("Attacking", attackId != 0);
        animator.SetInteger("AttackId", attackId);
        animator.SetBool("Charging", state == "ChargeUp" || state == "ChargeDown");
        animator.SetBool("ChargeDown", state == "ChargeDown");
    }
}
