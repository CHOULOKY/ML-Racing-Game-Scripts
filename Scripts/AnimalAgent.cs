using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class AnimalAgent : Agent
{
      [Tooltip("Start zero")] public int animalNumber;

      private Rigidbody rigid;

      private Vector3 startPosition;
      private Quaternion startRotation;

      public Transform[] startTransforms;
      private Vector3[] startPositions;
      private Quaternion[] startRotations;
      // [Tooltip("Inference Only")] public bool useModel;

      public GameObject startCheckpoint;
      private int startCheckpointIndex;
      public GameObject goalCheckpoint;
      private int goalCheckpointIndex;

      private int moveZ = 0;
      private int turnX = 0;
      public float moveSpeed;
      public float turnSpeed;

      private List<GameObject> checkpoints;
      private int nextCheckpoint = 0;
      private float checkpointTime = 0;
      [SerializeField] private float maxCheckpointTime;

      private float directionDot;

      private bool IsTrainingOrHeuristicOnly =>
          GameManager.Instance.mainCamera.isTraining || GetComponent<BehaviorParameters>().BehaviorType == BehaviorType.HeuristicOnly;

      private void Start()
      {
            rigid = GetComponent<Rigidbody>();

            startPosition = rigid.position;
            startRotation = rigid.rotation;
            startPositions = startTransforms.Select(t => t.position).ToArray();
            startRotations = startTransforms.Select(t => t.rotation).ToArray();

            checkpoints = GameManager.Instance.checkpoints.checkpoints;

            startCheckpointIndex = checkpoints.IndexOf(startCheckpoint);
            goalCheckpointIndex = checkpoints.IndexOf(goalCheckpoint);
      }

      public override void OnEpisodeBegin()
      {
            rigid.velocity = Vector3.zero;
            rigid.angularVelocity = Vector3.zero;

            if (!IsTrainingOrHeuristicOnly) {
                  // Use a completed model
                  rigid.position = startPosition;
                  rigid.rotation = startRotation;
            }
            else {
                  int randomPosition;
                  if (startPositions.Length > 0) {
                        // Real course
                        randomPosition = UnityEngine.Random.Range(0, startPositions.Length);
                        rigid.position = startPositions[randomPosition];
                        rigid.rotation = startRotations[randomPosition];
                  }
                  else {
                        // Left or Right course
                        randomPosition = UnityEngine.Random.Range(0, 4);
                        rigid.position = new Vector3(startPosition.x, startPosition.y, -4 + randomPosition * 2.3f);
                        rigid.rotation = startRotation;
                  }
            }

            nextCheckpoint = startCheckpointIndex;
            checkpointTime = 0;
      }

      public override void CollectObservations(VectorSensor sensor)
      {
            // sensor.AddObservation(rigid.position);
            // sensor.AddObservation(rigid.rotation);

            // sensor.AddObservation(rigid.velocity);
            // sensor.AddObservation(rigid.angularVelocity);

            // 체크포인트 방향 일치도
            Vector3 checkpointForward = checkpoints[nextCheckpoint].transform.forward;
            directionDot = Vector3.Dot(checkpointForward, transform.forward);
            sensor.AddObservation(directionDot);

            // 체크포인트와의 거리
            float distanceToCheckpoint = Vector3.Distance(transform.position, checkpoints[nextCheckpoint].transform.position);
            sensor.AddObservation(distanceToCheckpoint);

            // 체크포인트로의 좌우 방향
            // Vector3 directionToCheckpoint = checkpoints[nextCheckpoint].transform.position - transform.position;
            // directionToCheckpoint.y = 0; // Y축을 기준으로 회전하도록 Y 좌표는 무시
            // Vector3 rotationAxis = Vector3.Cross(transform.forward, directionToCheckpoint.normalized);
            // sensor.AddObservation(rotationAxis);
      }

      public override void OnActionReceived(ActionBuffers actions)
      {
            moveZ = actions.DiscreteActions[0]; // 0: 정지, 1: 앞, 2: 뒤
            turnX = actions.DiscreteActions[1]; // 0: 정지, 1: 오른, 2: 왼

            // 다음 체크포인트까지 제한시간 내에 다다르지 못하면 페널티와 에피소드 종료
            checkpointTime += Time.deltaTime;
            if (checkpointTime > maxCheckpointTime && IsTrainingOrHeuristicOnly) {
                  // Debug.Log("제한시간");
                  checkpointTime = 0;
                  AddReward(-10f);
                  EndEpisode();
            }


            // 시간당 페널티
            // AddReward(MaxStep != 0 ? -1f / MaxStep : 0);
            AddReward(-1f / 1000);

            // 속도 비례 보상
            // float speedReward = Mathf.Clamp(Mathf.Sqrt(rigid.velocity.magnitude) * 0.01f, 0, Mathf.Abs(stepPenalty / 2));
            // AddReward(speedReward);

            // 움직일 때만 방향 일치도 보상
            if (directionDot < 0 || directionDot > 0.9f) {
                  AddReward(directionDot * (moveZ == 1 ? 1 : 0) * 0.01f);
            }

            // 체크포인트와의 거리 보상
            // float distanceToCheckpoint = Vector3.Distance(transform.position, checkpoints[nextCheckpoint].transform.position);
            // AddReward(-distanceToCheckpoint * 0.0001f);
      }

      public override void Heuristic(in ActionBuffers actionsOut)
      {
            ActionSegment<int> discreteSegment = actionsOut.DiscreteActions;

            float moveZ = Input.GetAxisRaw("Vertical");
            float turnX = Input.GetAxisRaw("Horizontal");
            moveZ = moveZ == -1 ? 2 : moveZ;
            turnX = turnX == -1 ? 2 : turnX;

            discreteSegment[0] = (int)moveZ;
            discreteSegment[1] = (int)turnX;
      }

      private void FixedUpdate()
      {
            Vector3 moveDirection = transform.forward * (moveZ == 2 ? -1 : moveZ) * moveSpeed;
            rigid.velocity = moveDirection;

            float turnDirection = (turnX == 2 ? -1 : turnX) * turnSpeed;
            rigid.angularVelocity = new Vector3(0f, turnDirection, 0f);

            // 현재 누적 보상 출력
            if (Input.GetKeyDown(KeyCode.Space)) {
                  Debug.Log($"Current Reward: {GetCumulativeReward()} // {nextCheckpoint}");
            }
      }


      private void OnTriggerEnter(Collider other)
      {
            if (!other.gameObject.CompareTag("Checkpoint")) return;

            int checkpointIndex = checkpoints.IndexOf(other.gameObject);
            if (checkpointIndex == nextCheckpoint) {
                  AddReward(2f); // 올바른 체크포인트 도달 시 보상
                  checkpointTime = 0; // 체크포인트 제한시간 초기화
                                      // Debug.Log($"Current Reward: {GetCumulativeReward()} // {nextCheckpoint}");

                  // 완주 체크포인트 도달 시
                  if (checkpointIndex == goalCheckpointIndex) {
                        AddReward(20f); // 완주 보상
                        EndEpisode();
                  }
                  else {
                        nextCheckpoint++; // 다음 체크포인트로 업데이트
                  }
            }
            else {
                  AddReward(-1f); // 잘못된 체크포인트에 도달하면 페널티 부여
            }
      }

      private void OnCollisionEnter(Collision collision)
      {
            if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Animal")) {
                  // 태그에 따라 서로 다른 패널티를 부여
                  AddReward(collision.gameObject.CompareTag("Wall") ? -1f : -0.5f);
                  // Debug.Log($"Current Reward: {GetCumulativeReward()}");

                  // 누적 보상이 -30f 이하일 때 추가 패널티 부여 및 에피소드 종료
                  if (GetCumulativeReward() < -30f) {
                        AddReward(-10f);
                        if (IsTrainingOrHeuristicOnly) {
                              EndEpisode();
                        }
                  }

                  // OnEpisodeBegin();
                  // EndEpisode();
            }
      }

      private void OnCollisionStay(Collision collision)
      {
            if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Animal")) {
                  // 태그에 따라 서로 다른 패널티를 부여
                  AddReward(collision.gameObject.CompareTag("Wall") ? -0.1f : -0.05f);

                  // 누적 보상이 -30f 이하일 때 추가 패널티 부여 및 에피소드 종료
                  if (GetCumulativeReward() < -30f) {
                        AddReward(-10f);
                        if (IsTrainingOrHeuristicOnly) {
                              EndEpisode();
                        }
                  }
            }
      }

      private void OnCollisionExit(Collision collision)
      {
            if (collision.gameObject.CompareTag("Wall")) {
                  AddReward(0.5f);
            }
            else if (collision.gameObject.CompareTag("Animal")) {
                  AddReward(0.25f);
            }
      }
}
