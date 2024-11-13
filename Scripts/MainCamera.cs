using Unity.MLAgents.Policies;
using UnityEngine;

public class MainCamera : MonoBehaviour
{
      public bool isTraining = false;
      public bool isObserver = true;

      [SerializeField] private AnimalAgent[] animals;
      [SerializeField] private int curAnimalIndex;

      private Transform targetTransform;
      public Vector3 offsetPosition;
      public float targetDistance;
      public float damping = 5.0f;    // 카메라 이동 부드러움
      public float rotationDamping = 3.0f; // 카메라 회전 부드러움

      public GameObject[] courseCameras;
      public int curCameraIndex;

      private void Awake()
      {
            animals = new AnimalAgent[4];
            foreach (AnimalAgent agent in FindObjectsByType<AnimalAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
                  if (animals[agent.animalNumber] != null) continue;
                  animals[agent.animalNumber] = agent;
                  targetTransform = agent.transform;
                  curAnimalIndex = agent.animalNumber;
            }
      }

      private void Update()
      {
            if (isTraining) {
                  if (!courseCameras[curCameraIndex].activeSelf) {
                        courseCameras[curCameraIndex].SetActive(true);
                  }
                  return;
            }

            if (Input.GetKeyDown(KeyCode.BackQuote)) {
                  isObserver = !isObserver;
                  if (animals[curAnimalIndex].gameObject.GetComponent<BehaviorParameters>().BehaviorType == BehaviorType.HeuristicOnly) {
                        animals[curAnimalIndex].gameObject.GetComponent<BehaviorParameters>().BehaviorType = BehaviorType.InferenceOnly;
                  }
            }

            if (isObserver) {
                  if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {
                        // Debug.Log("LeftControl");
                        if (courseCameras[curCameraIndex].activeSelf) {
                              courseCameras[curCameraIndex].SetActive(false);
                        }
                        for (int i = 1; i <= 4; i++) {
                              if (Input.GetKeyDown(KeyCode.Alpha0 + i)) {
                                    // Debug.Log(i - 1);
                                    curAnimalIndex = i - 1;
                                    break;
                              }
                        }
                  }
                  else {
                        if (Input.GetKeyDown(KeyCode.Alpha0)) {
                              courseCameras[curCameraIndex].SetActive(true);
                        }
                        for (int i = 1; i <= 4; i++) {
                              if (Input.GetKeyDown(KeyCode.Alpha0 + i)) {
                                    Time.timeScale = i;
                                    break;
                              }
                        }
                  }
            }
            else {
                  if (courseCameras[curCameraIndex].activeSelf) {
                        courseCameras[curCameraIndex].SetActive(false);
                  }
                  for (int i = 1; i <= 4; i++) {
                        if (Input.GetKeyDown(KeyCode.Alpha0 + i)) {
                              var behaviorParams = animals[curAnimalIndex].gameObject.GetComponent<BehaviorParameters>();
                              behaviorParams.BehaviorType = BehaviorType.InferenceOnly;
                              curAnimalIndex = i - 1;
                              break;
                        }
                  }
                  if (animals[curAnimalIndex].gameObject.GetComponent<BehaviorParameters>().BehaviorType == BehaviorType.InferenceOnly) {
                        animals[curAnimalIndex].gameObject.GetComponent<BehaviorParameters>().BehaviorType = BehaviorType.HeuristicOnly;
                  }
            }
      }

      private void LateUpdate()
      {
            if (targetTransform == null || isTraining) return;

            targetTransform = animals[curAnimalIndex].transform;

            Vector3 targetPosition = targetTransform.position - targetTransform.forward * targetDistance + offsetPosition;
            transform.position = Vector3.Lerp(transform.position, targetPosition, damping * Time.deltaTime);

            transform.LookAt(targetTransform.position + Vector3.up * 1.5f);
      }
}
