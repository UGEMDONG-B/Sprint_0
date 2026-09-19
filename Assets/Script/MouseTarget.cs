using UnityEngine;

public class TentacleTargetController : MonoBehaviour
{
    public Camera mainCamera;
    
    [Header("꿈틀거림 셋팅")]
    public float wriggleSpeed = 5f;    // 꿈틀거리는 속도
    public float wriggleAmount = 0.5f; // 꿈틀거리는 범위

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
    }

    void Update()
    {
        // 1. 화면의 마우스 좌표를 3D 월드 좌표(바닥 기준)로 변환
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero); // Y축을 위로 하는 가상의 바닥 생성
        float rayDistance;
        
        if (groundPlane.Raycast(ray, out rayDistance))
        {
            // 마우스가 닿은 3D 공간상의 위치
            Vector3 mousePos = ray.GetPoint(rayDistance);
            
            // 2. Mathf.Sin을 활용하여 유기체가 숨쉬며 꿈틀거리는 듯한 오프셋 추가
            float wriggleX = Mathf.Sin(Time.time * wriggleSpeed) * wriggleAmount;
            float wriggleZ = Mathf.Cos(Time.time * wriggleSpeed * 0.8f) * wriggleAmount;
            
            // Target의 최종 위치 업데이트
            transform.position = mousePos + new Vector3(wriggleX, 0, wriggleZ);
        }
    }
}