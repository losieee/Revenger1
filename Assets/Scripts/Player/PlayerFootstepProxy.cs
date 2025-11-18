using UnityEngine;

public enum FootEnv { Indoor, Outdoor, InSerwer }

public class PlayerFootstepProxy : MonoBehaviour
{
    [SerializeField] Animator animator;
    [SerializeField] float runThreshold = 0.6f;
    [SerializeField] float minGapWalk = 0.25f;
    [SerializeField] float minGapRun = 0.16f;

    FootEnv _env = FootEnv.Indoor;
    int _lastFrame = -1;
    float _lastTime = -999f;

    enum Gait { Walk, Run }

    Gait CurrentGait()
    {
        var st = animator.GetCurrentAnimatorStateInfo(0);
        if (st.IsTag("Run")) return Gait.Run;
        if (st.IsTag("Walk")) return Gait.Walk;
        return animator.GetFloat("Speed") > runThreshold ? Gait.Run : Gait.Walk;
    }

    public void SetEnvironment(FootEnv env)
    {
        Debug.Log("환경 변경: " + env);
        _env = env;
    }

    void TryPlay(PlayerSfx sfx, float minGap)
    {
        if (Time.frameCount == _lastFrame) return;
        if (animator.IsInTransition(0)) return;
        if (Time.time - _lastTime < minGap) return;

        SoundManager.i?.PlaySFX(sfx, SfxBus.Effect, 1f);
        _lastFrame = Time.frameCount;
        _lastTime = Time.time;
    }

    public void OnLeftFootstep()
    {
        var gait = CurrentGait();

        if (_env == FootEnv.Indoor)
            TryPlay(gait == Gait.Run ? PlayerSfx.LeftRunIndoor : PlayerSfx.LeftWalkIndoor,
                    gait == Gait.Run ? minGapRun : minGapWalk);
        else if (_env == FootEnv.Outdoor)
            TryPlay(gait == Gait.Run ? PlayerSfx.LeftRunOutdoor : PlayerSfx.LeftWalkOutdoor,
                    gait == Gait.Run ? minGapRun : minGapWalk);
        else if (_env == FootEnv.InSerwer)
            TryPlay(gait == Gait.Run ? PlayerSfx.SerwerLeft : PlayerSfx.SerwerLeft,
                    gait == Gait.Run ? minGapRun : minGapWalk);
    }

    public void OnRightFootstep()
    {
        var gait = CurrentGait();

        if (_env == FootEnv.Indoor)
            TryPlay(gait == Gait.Run ? PlayerSfx.RightRunIndoor : PlayerSfx.RightWalkIndoor,
                    gait == Gait.Run ? minGapRun : minGapWalk);
        else if (_env == FootEnv.Outdoor)
            TryPlay(gait == Gait.Run ? PlayerSfx.RightRunOutdoor : PlayerSfx.RightWalkOutdoor,
                    gait == Gait.Run ? minGapRun : minGapWalk);
        else if (_env == FootEnv.InSerwer)
            TryPlay(gait == Gait.Run ? PlayerSfx.SerwerRight : PlayerSfx.SerwerRight,
                    gait == Gait.Run ? minGapRun : minGapWalk);
    }
}
