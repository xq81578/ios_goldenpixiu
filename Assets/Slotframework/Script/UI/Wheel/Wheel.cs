using System;
using DG.Tweening;
using UnityEngine;

public enum WheelRotateDirection
{
    Clockwise,
    CounterClockwise
}

public enum WheelStateEnum
{
    WHEEL_STOP = 1,
    WHEEL_IDLE,
    WHEEL_IDLE_ROTATE,
    WHEEL_ROTATE,   // 中獎開始旋轉
    WHEEL_ROTATE_ADD_SPEED, // 加速中
    WHEEL_TO_ROTATING,  // 設定維持最大速度旋轉
    WHEEL_ROTATING,  // 中間狀態，維持最大速度旋轉
    WHEEL_ROTATE_STOP_START,    // 開始停輪
    WHEEL_ROTATE_STOP_360,  // 停輪到剩下360度
    WHEEL_ROTATE_STOP_100,  // 停輪到剩下100度
    WHEEL_ROTATE_STOP_20,  // 停輪到剩下20度
    WHEEL_ROTATE_END,   // 完全停輪
}

public class Wheel : MonoBehaviour
{
    [Header("IDLE 狀態一圈走幾秒")]
    [SerializeField]
    public float _idleRotationSpeed = 10f;
    [Header("中獎時要加速到的最大轉速")]
    [SerializeField]
    public float _rotationMaxSpeed = 10f;
    [Header("中獎時加速到的最大轉速的時間(秒)")]
    [SerializeField]
    public int _rotationStartTime = 1;
    [Header("中獎時中間維持旋轉時間(秒)")]
    [SerializeField]
    public int _rotatingTime = 2;
    [Header("中獎時煞車轉停的圈數")]
    [SerializeField]
    public int _rotationEndCircles = 2;
    [Header("中獎獎項要停的角度 (預設 0 正上方)")]
    [SerializeField]
    public float _endAngle = 0.0f;
    [Header("獎項數量")]
    [SerializeField]
    public int _rewardCount = 8;

    [Header("旋轉方向")]
    [SerializeField]
    private WheelRotateDirection _wheelRotateDirection = WheelRotateDirection.CounterClockwise; // 輪盤轉向(順或逆時針)
    private WheelStateEnum _state; // 輪盤旋轉狀態
    private float _currntSpeed = 1.0f;  // 目前速度
    private float _targetRotation = 0.0f;   // 停輪角度 (遞減到0 表示已經到目標)
    private float _stopAngle = 0.0f;    // 要停輪的獎項角度
    private bool _isStartToStop = false;    // 是否已經開始煞車
    Tweener _idleTweener;
    Action<WheelStateEnum> _onWheelStateChange = null;

    // 初始化，設定要接收旋轉狀態的callback
    public void Init(Action<WheelStateEnum> stateCallback)
    {
        _onWheelStateChange = stateCallback;
    }

    private void ChangeState(WheelStateEnum newState)
    {
        if (_state == newState)
        {
            return;
        }

        _state = newState;
        if (_onWheelStateChange != null)
        {
            _onWheelStateChange(_state);
        }
    }

    public void ChangeToIdle()
    {
        ChangeState(WheelStateEnum.WHEEL_IDLE);
    }

    public void ChangeToStop()
    {
        if (_idleTweener != null)
        {
            _idleTweener.Kill();
        }
        ChangeState(WheelStateEnum.WHEEL_STOP);
    }

    public void StartRotate(int stopIndex)
    {
        if (_idleTweener != null)
        {
            _idleTweener.Kill();
        }
        float targetAngle = stopIndex * (360.0f / _rewardCount);
        ChangeState(WheelStateEnum.WHEEL_ROTATE);
        // LogUtils.Log("Wheel stopIndex: " + stopIndex + "  targetAngle:" + targetAngle);
        _stopAngle = targetAngle;
        _isStartToStop = false;
    }

    public void RotationToPositive()
    {
        transform.localRotation = Quaternion.identity;
    }

    void FixedUpdate()
    {
        switch (_state)
        {
            case WheelStateEnum.WHEEL_STOP:
            break;
            case WheelStateEnum.WHEEL_IDLE:
                IdleRotate();
                ChangeState(WheelStateEnum.WHEEL_IDLE_ROTATE);
            break;
            case WheelStateEnum.WHEEL_IDLE_ROTATE:
            break;
            case WheelStateEnum.WHEEL_ROTATE:
                BonusRotate();
                ChangeState(WheelStateEnum.WHEEL_ROTATE_ADD_SPEED);
            break;
            case WheelStateEnum.WHEEL_ROTATE_ADD_SPEED:
            break;
            case WheelStateEnum.WHEEL_TO_ROTATING:
                BonusRotating();
                ChangeState(WheelStateEnum.WHEEL_ROTATING);
            break;
            case WheelStateEnum.WHEEL_ROTATING:
            break;
            case WheelStateEnum.WHEEL_ROTATE_STOP_START:
            case WheelStateEnum.WHEEL_ROTATE_STOP_360:
            case WheelStateEnum.WHEEL_ROTATE_STOP_100:
            case WheelStateEnum.WHEEL_ROTATE_STOP_20:
                UpdateRotateStop();
            break;
            case WheelStateEnum.WHEEL_ROTATE_END:
            break;
        }
    }

    // 取得順時針與逆時針的角度
    private float GetRotateValue(float rotation)
    {
        switch (_wheelRotateDirection)
        {
            case WheelRotateDirection.Clockwise:
            return -rotation;
            case WheelRotateDirection.CounterClockwise:
            return rotation;
        }

        return rotation;
    }

    private void IdleRotate()
    {
        float rotate = GetRotateValue(360);
        _idleTweener = gameObject.transform.DORotate(new Vector3(0.0f, 0.0f, rotate), _idleRotationSpeed)
                                                .SetEase(Ease.Linear)
                                                .SetRelative(true)
                                                .SetLoops(-1, LoopType.Incremental);
    }

    // 抓出要停輪目標的角度
    private float getStopRotaion()
    {
        float curRotation = transform.rotation.eulerAngles.z;
        float targetRotation = _stopAngle + _endAngle;
        float diffRotation = (targetRotation - curRotation) % 360;

        return diffRotation;
    }

    // 取得加速度，順時針跟逆時針轉的加速度正負不一樣
    private float GetRotateSpeed()
    {
        switch (_wheelRotateDirection)
        {
            case WheelRotateDirection.Clockwise:
            return -_currntSpeed;
            case WheelRotateDirection.CounterClockwise:
            return _currntSpeed;
        }

        return _currntSpeed;
    }

    // 計算輪盤結束速度
    private void UpdateRotateStop()
    {
        // 已經轉到結束的角度
        if (_targetRotation < 0)
        {
            return;
        }

        // 旋轉輪盤
        gameObject.transform.Rotate(0.0f, 0.0f, GetRotateSpeed());
        // 減速
        _targetRotation -= _currntSpeed;
        // 計算目前的減速度，減速分三個階段 (<20度  <100度  <360度)
        if (_targetRotation < 20)
        {
            _currntSpeed -= _currntSpeed * 4 * Time.deltaTime;
            _currntSpeed = Math.Max(_currntSpeed, 0.2f);
            ChangeState(WheelStateEnum.WHEEL_ROTATE_STOP_20);
        }
        else if (_targetRotation < 100)
        {
            _currntSpeed -= _currntSpeed * 2 * Time.deltaTime;
            _currntSpeed = Math.Max(_currntSpeed, 0.5f);
            ChangeState(WheelStateEnum.WHEEL_ROTATE_STOP_100);
        }
        else if (_targetRotation < 360)
        {
            if (!_isStartToStop)
            {
                _isStartToStop = true;
                ChangeState(WheelStateEnum.WHEEL_ROTATE_STOP_360);
            }
            _currntSpeed -= _currntSpeed * Time.deltaTime;
            _currntSpeed = Math.Max(_currntSpeed, 3.0f);
        }

        // 已經轉到要停的位置
        if (_targetRotation < 0)
        {
            ChangeState(WheelStateEnum.WHEEL_ROTATE_END);
            return;
        }
    }

    // 旋轉結束，找出要停輪的目標角度，在Update時另外計算減速
    private void BonusRotateStop()
    {
        float diffRotation = getStopRotaion();
        float rotate = GetRotateValue(360);
        _targetRotation = Math.Abs(diffRotation + (rotate * _rotationEndCircles));

        ChangeState(WheelStateEnum.WHEEL_ROTATE_STOP_START);
    }

    // 加速後中間保持速度旋轉固定圈數
    private void BonusRotating()
    {
        // 使用DoTween維持一樣的速度旋轉物件
        DOTween.To(() => _currntSpeed, x => _currntSpeed = x, _rotationMaxSpeed, _rotatingTime)
            .OnUpdate(() => transform.Rotate(0.0f, 0.0f, GetRotateSpeed()) )
            .OnComplete(() => {
                BonusRotateStop();
            })
            .SetEase(Ease.Linear);
    }

    // 開始旋轉
    private void BonusRotate()
    {
        // 使用DoTween幫忙計算加速度與旋轉物件
        DOTween.To(() => _currntSpeed, x => _currntSpeed = x, _rotationMaxSpeed, _rotationStartTime)
            .OnUpdate(() => transform.Rotate(0.0f, 0.0f, GetRotateSpeed()) )
            .OnComplete(() => {
                ChangeState(WheelStateEnum.WHEEL_TO_ROTATING);
            })
            .SetEase(Ease.Linear);
    }
}
