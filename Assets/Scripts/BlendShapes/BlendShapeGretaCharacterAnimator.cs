using animationparameters;
using audioElements;
using autodeskcharacter;
using BlendShapes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
using thriftImpl;
using time;
using tools;
using UnityEngine;

public class BlendShapeGretaCharacterAnimator : MonoBehaviour
{
    static int NUMBER_OF_FAPS = Enum.GetNames(typeof(FAPType)).Length;
    static int NUMBER_OF_BAPS = 296;
    private List<autodeskcharacter.bapmapper.BapMapper> bapMappers;
    private ConcatenateJoints concatenator = new ConcatenateJoints();

    public bool sat = false;
    public float hipsDegrees = -90;
    public float kneesDegrees = 90;

    public bool distantConnection = true;
    public int FAP_RECEIVER_PORT = 9700;
    public int BAP_RECEIVER_PORT = 9070;
    public int AUDIO_RECEIVER_PORT = 9007;
    public int CMD_SENDER_PORT = 9912; 
    public string GretaServersHost = "localhost";

    public float UpperBodyAnimationEffect = 0.9f;
    public bool agentPlaying;
    public bool animateAgent = true;
    public string animationID = "noIdea";
    public string animationIDold = "noIdea";

    public TimeController characterTimer;
    public bool thriftConsumerOpened = false;

    public AudioFilePlayer audioFilePlayer;
    public FAPReceiver fapReceiver;
    public BAPReceiver bapReceiver;
    public AudioReceiver audioReceiver;
    public CommandSender commandSender;

    private AnimationParametersFrame lastFAPFrame;
    private AnimationParametersFrame lastBAPFrame;
    private int cptFrames;

    private static int ANIM_DELAY = 000;

    private AudioSource _currentAudioSource = null;

    public GameObject _characterMesh;





    Dictionary<string, (string name, float weight)[]> blendshapeMapping;

    private List<(SkinnedMeshRenderer mesh, Dictionary<string, int> cache)> modelBlendshapeMeshCaches;
    private Dictionary<string, Transform> boneCache;
    public UnityEngine.Object boneMappingProfile;

    private Dictionary<string, string> boneNameMapping;

    // Pre-computed blendshape mapping for applyFapFrame
    private struct BlendShapeEntry
    {
        public SkinnedMeshRenderer mesh;
        public int blendShapeIndex;
        public int phonemeIndex;
        public float weight;
    }
    private BlendShapeEntry[] fapEntries;

    public UnityEngine.Object blendShapeProfile;

    [Header("Arm Pose Correction")]
    public bool CC5Model = false;


    void Awake()
    {        
        // Temporary : fixes float errors format when communicating with greta (. vs ,)
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

        blendshapeMapping = ConfigFileReaders.FileReader(Application.streamingAssetsPath + "/" + blendShapeProfile.name + ".ini");
        boneNameMapping = ConfigFileReaders.BoneMappingReader(Application.streamingAssetsPath + "/" + boneMappingProfile.name + ".ini");

        InitCharacterMesh();

        // Build bone lookup cache once
        boneCache = new Dictionary<string, Transform>();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            boneCache[t.name] = t;

        changeTPoseToNPose();
        setUpSkeleton();

        Time.fixedDeltaTime = (float)0.04;
        characterTimer = new TimeController();
        characterTimer.setTimeMillis(0);

        bapReceiver = new BAPReceiver(NUMBER_OF_BAPS, BAP_RECEIVER_PORT);
        fapReceiver = new FAPReceiver(NUMBER_OF_FAPS + 1, FAP_RECEIVER_PORT);
        audioReceiver = new AudioReceiver(AUDIO_RECEIVER_PORT);

        lastFAPFrame = new AnimationParametersFrame(NUMBER_OF_FAPS + 1, 0);
        lastBAPFrame = new AnimationParametersFrame(NUMBER_OF_BAPS, 0);
        cptFrames = 0;
        commandSender = new CommandSender(GretaServersHost, CMD_SENDER_PORT);
        agentPlaying = false;

        audioFilePlayer = new AudioFilePlayer();
        _currentAudioSource = gameObject.AddComponent<AudioSource>();


        modelBlendshapeMeshCaches = new List<(SkinnedMeshRenderer mesh, Dictionary<string, int> cache)>();

        var allMeshesForBlendshapes = GetComponentsInChildren<SkinnedMeshRenderer>(true);

        foreach (var mesh in allMeshesForBlendshapes)
        {
            if (mesh == null || mesh.sharedMesh == null || mesh.sharedMesh.blendShapeCount == 0)
                continue;

            var cache = new Dictionary<string, int>();

            foreach (var kvp in blendshapeMapping)
            {
                foreach (var (bsName, _) in kvp.Value)
                {
                    if (cache.ContainsKey(bsName)) continue;
                    int idx = mesh.sharedMesh.GetBlendShapeIndex(bsName);
                    if (idx >= 0)
                        cache[bsName] = idx;
                }
            }

            if (cache.Count > 0)
                modelBlendshapeMeshCaches.Add((mesh, cache));
        }

        // Pre-compute flat FAP entries for applyFapFrame
        var fapList = new List<BlendShapeEntry>();
        int pIdx = 0;
        foreach (var kvp in blendshapeMapping)
        {
            foreach (var (bsName, bsWeight) in kvp.Value)
            {
                foreach (var (mesh, cache) in modelBlendshapeMeshCaches)
                {
                    if (cache.TryGetValue(bsName, out int idx) && idx >= 0)
                    {
                        fapList.Add(new BlendShapeEntry
                        {
                            mesh = mesh,
                            blendShapeIndex = idx,
                            phonemeIndex = pIdx,
                            weight = bsWeight
                        });
                    }
                }
            }
            pIdx++;
        }
        fapEntries = fapList.ToArray();

    }

    protected void InitCharacterMesh()
    {
        if (_characterMesh == null || !_characterMesh.activeSelf)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform t = transform.GetChild(i);
                if (t.gameObject.activeSelf)
                {
                    _characterMesh = t.gameObject;
                    break;
                }
            }
        }
        if (_characterMesh == null)
            Debug.LogError("No characterMesh assigned and no active child found");
    }

    public void FixedUpdate()
    {
        if (animateAgent)
        {
            AnimationParametersFrame currentFAPFrame = null;
            AnimationParametersFrame currentBAPFrame = null;

            AudioElement currentAudio = null;

            if (distantConnection)
            {
                if (!thriftConsumerOpened)
                {

                    if (!fapReceiver.isConnected() && !fapReceiver.isOnConnection())
                    {
                        fapReceiver.startConnection();
                    }
                    else if (!bapReceiver.isConnected() && !bapReceiver.isOnConnection() && fapReceiver.isConnected())
                    {
                        Debug.Log("FAP Receiver started");
                        bapReceiver.startConnection();
                    }
                    else if (!audioReceiver.isConnected() && !audioReceiver.isOnConnection() && bapReceiver.isConnected())
                    {
                        Debug.Log("BAP Receiver started");
                        audioReceiver.startConnection();
                    }
                    else if (!commandSender.isConnected() && !commandSender.isOnConnection() && audioReceiver.isConnected())
                    {
                        Debug.Log("Audio Receiver started");
                        commandSender.startConnection();
                    }
                    else if (commandSender.isConnected())
                    {
                        Debug.Log("Connection Sender started");
                        thriftConsumerOpened = true;
                    }
                }
                else
                {
                    if (fapReceiver.timer.isSynchronized())
                    {
                        characterTimer.setTimeMillis(fapReceiver.timer.getTimeMillis() - ANIM_DELAY);
                        SceneManager.gretaClock = (float)characterTimer.getTimeMillis();
                        currentFAPFrame = fapReceiver.getCurrentFrame(characterTimer.getTimeMillis() / 40);
                        //Debug.Log(currentFAPFrame.AnimationParametersFrame2String(0));


                    }
                    if (bapReceiver.timer.isSynchronized())
                    {
                        if (SceneManager.gretaClock <= 0)
                        {
                            characterTimer.setTimeMillis(bapReceiver.timer.getTimeMillis() - ANIM_DELAY);
                            SceneManager.gretaClock = (float)(characterTimer.getTimeMillis());
                        }
                        currentBAPFrame = bapReceiver.getCurrentFrame(characterTimer.getTimeMillis() / 40);
                    }
                    if (fapReceiver.timer.isSynchronized())
                    {
                        currentAudio = audioReceiver.getCurrentAudioElement(characterTimer.getTimeMillis() / 40);
                    }
                }
            }
            else
            {
                if (fapReceiver.isConnected())
                {
                    fapReceiver.stopConnector();
                    thriftConsumerOpened = false;
                }
                if (bapReceiver.isConnected())
                {
                    bapReceiver.stopConnector();
                    thriftConsumerOpened = false;
                }
                if (audioReceiver.isConnected())
                {
                    audioReceiver.stopConnector();
                    thriftConsumerOpened = false;
                }
            }

            if (currentFAPFrame != null)
            {
                if (lastFAPFrame.isEqualTo(currentFAPFrame))
                {
                    cptFrames++;
                    if (cptFrames > 2)
                    {
                        agentPlaying = false;
                        cptFrames = 0;
                    }
                }
                else
                {
                    agentPlaying = true;
                    cptFrames = 0;
                    lastFAPFrame = new AnimationParametersFrame(currentFAPFrame);
                }


                    applyFapFrame(currentFAPFrame);
            }

            if (currentBAPFrame != null)
            {
                if (!lastBAPFrame.isEqualTo(currentBAPFrame))
                    lastBAPFrame = new AnimationParametersFrame(currentBAPFrame);
                applyBapFrame(currentBAPFrame);
            }

            /*EB : START TEST FOR AUDIO BUFFER*/
            if (audioFilePlayer.isNewAudio() || audioReceiver.isNewAudio())
            {
                //EB : I reconstructed the short values computed by cereproc from the byte buffer sent by VIB
                // and used this short value to fill the float buffer needed by the audio clip
                if (currentAudio.getSampleRate() > 0 && currentAudio.rawData.Length > 0)
                {
                    int len = currentAudio.rawData.Length / 2;
                    //EB: I couldn't find in Unity how to clean an audio clip nor how to modify its buffer length,
                    // so I prefered to destroy the audio clip (to free the memory) and to create an audio clip
                    // which has the appropriate float buffer size.
                    // In theory the frequency should be provided by the currentAudio object (which should
                    // receive such an information in the message from VIB), but since this is not the case
                    // I hard coded the frequency (47250). It works fine with cereproc, but not with MaryTTS.
                    // For Mary you need to set the frequency to 16000. This is ugly, really!
                    // It should be a input and not hard coded. The problem is that the thrift message doesn't
                    // contain the information at all and I don't want to put my hands in that part of your code.
                    Destroy(_currentAudioSource.clip);

                    _currentAudioSource.clip = AudioClip.Create("text", len, 1, currentAudio.getSampleRate(), false);
                    float[] buffer = new float[len];
                    for (int iPCM = 44; iPCM < len; iPCM++)
                    {
                        float f;
                        short i = (short)((currentAudio.rawData[iPCM * 2 + 1] << 8) | currentAudio.rawData[iPCM * 2]);
                        f = ((float)i) / (float)32768;
                        if (f > 1) f = 1;
                        if (f < -1) f = -1;
                        buffer[iPCM] = f;
                    }
                    _currentAudioSource.clip.SetData(buffer, 0);
                    _currentAudioSource.Play();

                    audioReceiver.setNewAudio(false);
                    audioFilePlayer.setNewAudio(false);
                }
                else
                {
                    if ((_currentAudioSource != null) && (_currentAudioSource.clip != null))
                    {
                        float offSet = ((float)characterTimer.getTimeMillis() - ((float)currentAudio.getFrameNumber() * 40)) / 1000;
                        int samplesOffset = (int)(_currentAudioSource.clip.frequency * offSet * _currentAudioSource.clip.channels);
                        _currentAudioSource.timeSamples = samplesOffset;
                        _currentAudioSource.Play();
                    }
                    audioReceiver.setNewAudio(false);
                    audioFilePlayer.setNewAudio(false);
                }
            }
        }
        else
        {
            if (_currentAudioSource != null)
            {
                _currentAudioSource.Stop();
            }
        }
        if (animationIDold != animationID)
        {
            PlayAgentAnimation(animationID);
            animationIDold = animationID;
        }
    }

    public void OnApplicationQuit()
    {
        if (SceneManager.isThrift)
        {
            if (fapReceiver.isConnected())
            {
                fapReceiver.stopConnector();
            }
            if (bapReceiver.isConnected())
            {
                bapReceiver.stopConnector();
            }
            if (audioReceiver.isConnected())
            {
                audioReceiver.stopConnector();
            }
            if (commandSender.isConnected())
            {
                commandSender.stopConnector();
            }
        }
    }

    public void PlayAgentAnimation(string animationID, InterpersonalAttitude attitude = null)
    {
        animateAgent = true;
        if (distantConnection)
        {
            if (commandSender.isConnected())
            {
                commandSender.playAnimation(animationID, attitude);
            }
            else
            {
                Debug.LogWarning("AnimationReceiver on host: " + commandSender.getHost() + " and port: " + commandSender.getPort() + " not connected");
            }
        }
    }

    public void PlayAgentAnimation(string animationID)
    {
        animateAgent = true;
        if (distantConnection)
        {
            if (commandSender.isConnected())
                commandSender.playAnimation(animationID);
            else
                Debug.LogWarning("AnimationReceiver on host: " + commandSender.getHost() + " and port: " + commandSender.getPort() + " not connected");
        }
    }
    public Transform findBone(string name)
    {
        if (boneCache != null && boneCache.TryGetValue(name, out Transform t))
            return t;
        return null;
    }

    public Transform getBone(string gretaName)
    {
        string realName = gretaName;
        if (boneNameMapping != null && boneNameMapping.TryGetValue(gretaName, out string mapped))
            realName = mapped;
        return findBone(realName);
    }

    public bool hasBone(string gretaName)
    {
        return getBone(gretaName) != null;
    }

    private void setUpSkeleton()
    {
        Quaternion oldRotation = transform.rotation;
        Vector3 oldPosition = transform.position;
        transform.rotation = Quaternion.identity;
        transform.position = Vector3.zero;

        if (sat)
        {
            correctBone("LeftLeg",   Quaternion.AngleAxis(kneesDegrees, new Vector3(0, 1, 0)));
            correctBone("RightLeg",  Quaternion.AngleAxis(kneesDegrees, new Vector3(0, 1, 0)));
            correctBone("LeftUpLeg", Quaternion.AngleAxis(hipsDegrees,  new Vector3(1, 0, 0)));
            correctBone("RightUpLeg",Quaternion.AngleAxis(hipsDegrees,  new Vector3(1, 0, 0)));
        }

        bapMappers = new List<autodeskcharacter.bapmapper.BapMapper>();
        setupBapMapper();

        transform.position = oldPosition;
        transform.rotation = oldRotation;
    }

    private void changeTPoseToNPose()
    {
        float clavC = 9;
        Quaternion rclavC   = Quaternion.AngleAxis(clavC, new Vector3(0, 0,  1));
        Quaternion lclavC   = Quaternion.AngleAxis(clavC, new Vector3(0, 0, -1));

        Quaternion rShoulderC;
        Quaternion lShoulderC;
        Quaternion rThumbC;
        Quaternion lThumbC;

        if (CC5Model)
        {
            rShoulderC = Quaternion.AngleAxis(90 - clavC, new Vector3(0, 0, -1));
            lShoulderC = Quaternion.AngleAxis(90 - clavC, new Vector3(0, 0,  1));

            rThumbC =
                Quaternion.AngleAxis( 33, new Vector3(0, 1, 0)) *
                Quaternion.AngleAxis(20, new Vector3(0, 0, 1)) *
                Quaternion.AngleAxis( 15, new Vector3(1, 0, 0));
            lThumbC =
                Quaternion.AngleAxis(-33, new Vector3(0, 1, 0)) *
                Quaternion.AngleAxis(-20, new Vector3(0, 0, 1)) *
                Quaternion.AngleAxis(15, new Vector3(1, 0, 0));
        }
        else
        {
            rShoulderC = Quaternion.AngleAxis(90 - clavC, new Vector3(0, 0, 1));
            lShoulderC = Quaternion.AngleAxis(90 - clavC, new Vector3(0, 0, 1));

            rThumbC =
                Quaternion.AngleAxis(-33, new Vector3(0, 1, 0)) *
                Quaternion.AngleAxis(-20, new Vector3(0, 0, 1)) *
                Quaternion.AngleAxis( 15, new Vector3(1, 0, 0));
            lThumbC = rThumbC;
        }

        correctBone("RightShoulder",   rclavC);
        correctBone("LeftShoulder",    lclavC);
        correctBone("RightArm",        rShoulderC);
        correctBone("LeftArm",         lShoulderC);
        correctBone("RightHandThumb1", rThumbC);
        correctBone("LeftHandThumb1",  lThumbC);

        if (CC5Model)
        {
            correctBone("RightArm", Quaternion.Euler(0f, 15f, -15f));
            correctBone("LeftArm",  Quaternion.Euler(0f, -15f,  15f));
        }
    }

    private void setupBapMapper()
    {
        List<JointType> typesUsed = new List<JointType>();

        // spine
        map(typesUsed, "Hips",   JointType.HumanoidRoot);
        map(typesUsed, "Head",   JointType.skullbase);
        map(typesUsed, "Neck1",  JointType.vc5);
        map(typesUsed, "Neck",   JointType.vc7);
        map(typesUsed, "Spine4", JointType.vt6);
        map(typesUsed, "Spine3", JointType.vt12);
        map(typesUsed, "Spine2", JointType.vl1);
        map(typesUsed, "Spine1", JointType.vl3);
        map(typesUsed, "Spine",  JointType.vl5);
        map(typesUsed, "Spine2V", JointType.vl1, -0.5);
        map(typesUsed, "Spine1V", JointType.vl3, -0.5);
        map(typesUsed, "SpineV",  JointType.vl5, -0.5);

        // legs
        map(typesUsed, "LeftUpLeg",  "LeftUpLegRoll",  JointType.l_hip,    0.5, false);
        map(typesUsed, "LeftLeg",    JointType.l_knee);
        map(typesUsed, "LeftFoot",   "LeftLegRoll",    JointType.l_ankle,  0.5, true);
        map(typesUsed, "LeftToeBase", JointType.l_midtarsal);

        map(typesUsed, "RightUpLeg", "RightUpLegRoll", JointType.r_hip,    0.5, false);
        map(typesUsed, "RightLeg",   JointType.r_knee);
        map(typesUsed, "RightFoot",  "RightLegRoll",   JointType.r_ankle,  0.5, true);
        map(typesUsed, "RightToeBase", JointType.r_midtarsal);

        // right arm
        map(typesUsed, "RightShoulder", JointType.r_sternoclavicular);
        mapShoulder(typesUsed, "RightArm", "RightArmRoll", JointType.r_shoulder, JointType.r_acromioclavicular, 0.5);
        map(typesUsed, "RightForeArm", JointType.r_elbow);
        map(typesUsed, "RightHand", "RightForeArmRoll", JointType.r_wrist, 0.75, true);

        map(typesUsed, "RightHandThumb1", JointType.r_thumb1, CC5Model);
        map(typesUsed, "RightHandThumb2", JointType.r_thumb2);
        map(typesUsed, "RightHandThumb3", JointType.r_thumb3);

        map(typesUsed, "RightHandIndex0", JointType.r_index0);
        map(typesUsed, "RightHandIndex1", JointType.r_index1);
        map(typesUsed, "RightHandIndex2", JointType.r_index2);
        map(typesUsed, "RightHandIndex3", JointType.r_index3);

        map(typesUsed, "RightHandMiddle1", JointType.r_middle1);
        map(typesUsed, "RightHandMiddle2", JointType.r_middle2);
        map(typesUsed, "RightHandMiddle3", JointType.r_middle3);

        map(typesUsed, "RightHandRing1", JointType.r_ring1);
        map(typesUsed, "RightHandRing2", JointType.r_ring2);
        map(typesUsed, "RightHandRing3", JointType.r_ring3);

        map(typesUsed, "RightHandPinky0", JointType.r_pinky0);
        map(typesUsed, "RightHandPinky1", JointType.r_pinky1);
        map(typesUsed, "RightHandPinky2", JointType.r_pinky2);
        map(typesUsed, "RightHandPinky3", JointType.r_pinky3);

        // left arm
        map(typesUsed, "LeftShoulder", JointType.l_sternoclavicular);
        mapShoulder(typesUsed, "LeftArm", "LeftArmRoll", JointType.l_shoulder, JointType.l_acromioclavicular, 0.5);
        map(typesUsed, "LeftForeArm", JointType.l_elbow);
        map(typesUsed, "LeftHand", "LeftForeArmRoll", JointType.l_wrist, 0.75, true);

        map(typesUsed, "LeftHandThumb1", JointType.l_thumb1);
        map(typesUsed, "LeftHandThumb2", JointType.l_thumb2);
        map(typesUsed, "LeftHandThumb3", JointType.l_thumb3);

        map(typesUsed, "LeftHandIndex0", JointType.l_index0);
        map(typesUsed, "LeftHandIndex1", JointType.l_index1);
        map(typesUsed, "LeftHandIndex2", JointType.l_index2);
        map(typesUsed, "LeftHandIndex3", JointType.l_index3);

        map(typesUsed, "LeftHandMiddle1", JointType.l_middle1);
        map(typesUsed, "LeftHandMiddle2", JointType.l_middle2);
        map(typesUsed, "LeftHandMiddle3", JointType.l_middle3);

        map(typesUsed, "LeftHandRing1", JointType.l_ring1);
        map(typesUsed, "LeftHandRing2", JointType.l_ring2);
        map(typesUsed, "LeftHandRing3", JointType.l_ring3);

        map(typesUsed, "LeftHandPinky0", JointType.l_pinky0);
        map(typesUsed, "LeftHandPinky1", JointType.l_pinky1);
        map(typesUsed, "LeftHandPinky2", JointType.l_pinky2);
        map(typesUsed, "LeftHandPinky3", JointType.l_pinky3);

        concatenator.setJointToUse(typesUsed);
    }

    private void map(List<JointType> typesUsed, String boneName, JointType joint)
    {
        map(typesUsed, boneName, null, joint, 0, true, 1);
    }

    private void map(List<JointType> typesUsed, String boneName, JointType joint, bool invertY)
    {
        if (!hasBone(boneName)) return;
        if (!invertY)
        {
            map(typesUsed, boneName, null, joint, 0, true, 1);
            return;
        }

        List<Vector3> dofs  = new List<Vector3>(3);
        List<BAPType> types = new List<BAPType>(3);
        if (joint.rotationX != BAPType.null_bap) { dofs.Add(new Vector3(1, 0, 0));  types.Add(joint.rotationX); }
        if (joint.rotationY != BAPType.null_bap) { dofs.Add(new Vector3(0, 1, 0));  types.Add(joint.rotationY); }
        if (joint.rotationZ != BAPType.null_bap) { dofs.Add(new Vector3(0, 0, -1)); types.Add(joint.rotationZ); }

        if (dofs.Count == 0) return;
        if (dofs.Count == 1)
            bapMappers.Add(new autodeskcharacter.bapmapper.OneDOF(getBone(boneName), types[0], dofs[0]));
        else if (dofs.Count == 2)
            bapMappers.Add(new autodeskcharacter.bapmapper.TwoDOF(getBone(boneName), types[0], dofs[0], types[1], dofs[1]));
        else
            bapMappers.Add(new autodeskcharacter.bapmapper.ThreeDOF(getBone(boneName), types[0], dofs[0], types[1], dofs[1], types[2], dofs[2]));

        typesUsed.Add(joint);
    }

    private void map(List<JointType> typesUsed, String boneName, JointType joint, double scale)
    {
        map(typesUsed, boneName, null, joint, 0, true, scale);
    }

    private void map(List<JointType> typesUsed, String boneName, String twistBoneName, JointType joint, double twistFactor, bool before)
    {
        map(typesUsed, boneName, twistBoneName, joint, twistFactor, before, 1);
    }

    private void map(List<JointType> typesUsed, String boneName, String twistBoneName, JointType joint, double twistFactor, bool before, double scale)
    {
        if (!hasBone(boneName)) return;

        List<Vector3> dofs  = new List<Vector3>(3);
        List<BAPType> types = new List<BAPType>(3);
        if (joint.rotationX != BAPType.null_bap) { dofs.Add(new Vector3(1, 0, 0));  types.Add(joint.rotationX); }
        if (joint.rotationY != BAPType.null_bap) { dofs.Add(new Vector3(0, -1, 0)); types.Add(joint.rotationY); }
        if (joint.rotationZ != BAPType.null_bap) { dofs.Add(new Vector3(0, 0, -1)); types.Add(joint.rotationZ); }

        if (dofs.Count == 0) return;

        if (dofs.Count == 1)
        {
            bapMappers.Add(new autodeskcharacter.bapmapper.OneDOF(getBone(boneName), types[0], dofs[0]));
        }
        else if (dofs.Count == 2)
        {
            bapMappers.Add(new autodeskcharacter.bapmapper.TwoDOF(getBone(boneName), types[0], dofs[0], types[1], dofs[1]));
        }
        else // 3 DOF
        {
            if (twistBoneName != null && hasBone(twistBoneName))
            {
                if (before)
                    bapMappers.Add(new autodeskcharacter.bapmapper.YawTwistBeforeMapper(getBone(boneName), getBone(twistBoneName), types[0], dofs[0], types[1], dofs[1], types[2], dofs[2], twistFactor));
                else
                    bapMappers.Add(new autodeskcharacter.bapmapper.YawTwistAfterMapper(getBone(boneName), getBone(twistBoneName), types[0], dofs[0], types[1], dofs[1], types[2], dofs[2], twistFactor));
            }
            else if (scale == 1)
            {
                bapMappers.Add(new autodeskcharacter.bapmapper.ThreeDOF(getBone(boneName), types[0], dofs[0], types[1], dofs[1], types[2], dofs[2]));
            }
            else
            {
                var bm = new autodeskcharacter.bapmapper.ThreeDOFScaled(getBone(boneName), types[0], dofs[0], types[1], dofs[1], types[2], dofs[2]);
                bm.setScale(scale);
                bapMappers.Add(bm);
            }
        }
        typesUsed.Add(joint);
    }

    private void mapShoulder(List<JointType> typesUsed, String boneName, String twistBoneName, JointType shoulderJoint, JointType acromiumJoint, double twistFactor)
    {
        if (!hasBone(boneName)) return;

        List<Vector3> dofs  = new List<Vector3>(3) { new Vector3(1,0,0), new Vector3(0,-1,0), new Vector3(0,0,-1) };
        List<BAPType> types = new List<BAPType>(3) { shoulderJoint.rotationX, shoulderJoint.rotationY, shoulderJoint.rotationZ };

        if (hasBone(twistBoneName))
            bapMappers.Add(new autodeskcharacter.bapmapper.YawTwistAfterMapper(getBone(boneName), getBone(twistBoneName), types[0], dofs[0], types[1], dofs[1], types[2], dofs[2], twistFactor));
        else
            bapMappers.Add(new autodeskcharacter.bapmapper.ThreeDOF(getBone(boneName), types[0], dofs[0], types[1], dofs[1], types[2], dofs[2]));

        typesUsed.Add(shoulderJoint);
    }

    private void correctBone(string boneName, Quaternion correction)
    {
        Transform bone = getBone(boneName);
        if (bone != null)
            bone.localRotation = correction * bone.localRotation;
    }

    public void applyBapFrame(AnimationParametersFrame bapframe)
    {
        bapframe = concatenator.concatenateJoints(bapframe);
        foreach (autodeskcharacter.bapmapper.BapMapper mapper in bapMappers)
            mapper.applyBap(bapframe);
    }


    public void applyFapFrame(AnimationParametersFrame fapFrame)
    {
        for (int i = 0; i < fapEntries.Length; i++)
        {
            ref BlendShapeEntry e = ref fapEntries[i];
            float value = fapFrame.getValue(e.phonemeIndex);
            e.mesh.SetBlendShapeWeight(e.blendShapeIndex, value * e.weight);
        }
    }
}