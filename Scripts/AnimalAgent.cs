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

            // üũ����Ʈ ���� ��ġ��
            Vector3 checkpointForward = checkpoints[nextCheckpoint].transform.forward;
            directionDot = Vector3.Dot(checkpointForward, transform.forward);
            sensor.AddObservation(directionDot);

            // üũ����Ʈ���� �Ÿ�
            float distanceToCheckpoint = Vector3.Distance(transform.position, checkpoints[nextCheckpoint].transform.position);
            sensor.AddObservation(distanceToCheckpoint);

            // üũ����Ʈ���� �¿� ����
            // Vector3 directionToCheckpoint = checkpoints[nextCheckpoint].transform.position - transform.position;
            // directionToCheckpoint.y = 0; // Y���� �������� ȸ���ϵ��� Y ��ǥ�� ����
            // Vector3 rotationAxis = Vector3.Cross(transform.forward, directionToCheckpoint.normalized);
            // sensor.AddObservation(rotationAxis);
      }

      public override void OnActionReceived(ActionBuffers actions)
      {
            moveZ = actions.DiscreteActions[0]; // 0: ����, 1: ��, 2: ��
            turnX = actions.DiscreteActions[1]; // 0: ����, 1: ����, 2: ��

            // ���� üũ����Ʈ���� ���ѽð� ���� �ٴٸ��� ���ϸ� ���Ƽ�� ���Ǽҵ� ����
            checkpointTime += Time.deltaTime;
            if (checkpointTime > maxCheckpointTime && IsTrainingOrHeuristicOnly) {
                  // Debug.Log("���ѽð�");
                  checkpointTime = 0;
                  AddReward(-10f);
                  EndEpisode();
            }


            // �ð��� ���Ƽ
            // AddReward(MaxStep != 0 ? -1f / MaxStep : 0);
            AddReward(-1f / 1000);

            // �ӵ� ��� ����
            // float speedReward = Mathf.Clamp(Mathf.Sqrt(rigid.velocity.magnitude) * 0.01f, 0, Mathf.Abs(stepPenalty / 2));
            // AddReward(speedReward);

            // ������ ���� ���� ��ġ�� ����
            if (directionDot < 0 || directionDot > 0.9f) {
                  AddReward(directionDot * (moveZ == 1 ? 1 : 0) * 0.01f);
            }

            // üũ����Ʈ���� �Ÿ� ����
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

            // ���� ���� ���� ���
            if (Input.GetKeyDown(KeyCode.Space)) {
                  Debug.Log($"Current Reward: {GetCumulativeReward()} // {nextCheckpoint}");
            }
      }


      private void OnTriggerEnter(Collider other)
      {
            if (!other.gameObject.CompareTag("Checkpoint")) return;

            int checkpointIndex = checkpoints.IndexOf(other.gameObject);
            if (checkpointIndex == nextCheckpoint) {
                  AddReward(2f); // �ùٸ� üũ����Ʈ ���� �� ����
                  checkpointTime = 0; // üũ����Ʈ ���ѽð� �ʱ�ȭ
                                      // Debug.Log($"Current Reward: {GetCumulativeReward()} // {nextCheckpoint}");

                  // ���� üũ����Ʈ ���� ��
                  if (checkpointIndex == goalCheckpointIndex) {
                        AddReward(20f); // ���� ����
                        EndEpisode();
                  }
                  else {
                        nextCheckpoint++; // ���� üũ����Ʈ�� ������Ʈ
                  }
            }
            else {
                  AddReward(-1f); // �߸��� üũ����Ʈ�� �����ϸ� ���Ƽ �ο�
            }
      }

      private void OnCollisionEnter(Collision collision)
      {
            if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Animal")) {
                  // �±׿� ���� ���� �ٸ� �г�Ƽ�� �ο�
                  AddReward(collision.gameObject.CompareTag("Wall") ? -1f : -0.5f);
                  // Debug.Log($"Current Reward: {GetCumulativeReward()}");

                  // ���� ������ -30f ������ �� �߰� �г�Ƽ �ο� �� ���Ǽҵ� ����
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
                  // �±׿� ���� ���� �ٸ� �г�Ƽ�� �ο�
                  AddReward(collision.gameObject.CompareTag("Wall") ? -0.1f : -0.05f);

                  // ���� ������ -30f ������ �� �߰� �г�Ƽ �ο� �� ���Ǽҵ� ����
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
