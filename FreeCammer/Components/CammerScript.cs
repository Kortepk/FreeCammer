using BepInEx;
using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using static UnityEngine.InputSystem.DefaultInputActions;

namespace FreeCammer.Scripts
{
    internal class CammerScript : MonoBehaviour
    {
        public static CammerScript instance;
        public bool inCammer = false;
        public float moveSpeed = 1000f;
        public float intensity = 50f;
        public float speedMult = 500f;
        public Camera cammer;
        public AudioListener ears;
        public Light light;
        public PlayerControllerB me;
        public GameObject lightHolder;
        private GameObject myarms;
        private Renderer myhead;
        public LODGroup lodgroup;
        public GameObject lod1;
        public GameObject lod2;
        public GameObject lod3;
        public Volume volume;
        public Fog fog;
        private bool killme;
        public List<string> keyList = MyCammer.camControls.Value.Split(' ').ToList();
        public List<string> intensKeyList = MyCammer.intensityControls.Value.Split(' ').ToList();
        public int beforeCamLayerVal;
        public Camera beforeCamCamera;
        private Vector3 targetEulerRotation;
        private Vector3 currentEulerRotation;
        public float mouseSensitivity = 4.0f;    // Mouse sensitivity

        private Vector3 currentVelocity;

        public void Awake()
        {
            MyCammer.mls.LogInfo("Script Active");
            instance = this;
        }


        // Retrieves the mouse look sensitivity from the game's settings.
        // Falls back to default value if settings aren't available.
        private float GetGameLookSensitivity()
        {
            // First check if the game settings instance exists and is initialized
            if (IngamePlayerSettings.Instance != null && IngamePlayerSettings.Instance.settings != null)
            {
                // Return the actual sensitivity value from game settings
                return IngamePlayerSettings.Instance.settings.lookSensitivity;
            }

            // Fallback to default sensitivity (10f matches the game's base value)
            return 10f;
        }

        public void Update() 
        {
            
            if(volume == null)
            {
                foreach (Volume v in FindObjectsOfType<Volume>().ToList())
                {
                    if (v.gameObject.name == "VolumeMain")
                    {
                        volume = v;
                    }
                }
                if(fog == null && volume!= null)
                {
                    volume.sharedProfile.TryGet(out fog);
                }
            }
            //lastFrameX = x;
            //lastFrameY = y;
            //x = UnityInput.Current.mousePosition.x;
            //y = UnityInput.Current.mousePosition.y;
            //mouseDeltaX = x - lastFrameX;
            //mouseDeltaY = y - lastFrameY;
            //MyCammer.mls.LogInfo(Mouse.current.delta.value);
            if(me == null)
            {
                foreach(PlayerControllerB p in FindObjectsOfType<PlayerControllerB>())
                {
                    if(p.IsOwner)
                    {
                        me = p;
                        myarms = me.cameraContainerTransform.parent.GetChild(1).gameObject;
                        myhead = GameObject.Find("Systems/Rendering/PlayerHUDHelmetModel/ScavengerHelmet").GetComponent<Renderer>();
                        if (me.GetComponentInChildren<LODGroup>())
                        {
                            lodgroup = me.GetComponentInChildren<LODGroup>();
                            foreach(var lod in lodgroup.GetLODs())
                            {
                                foreach(var lodagain in lod.renderers)
                                {
                                    if (lodagain.gameObject.name == "LOD1") lod1 = lodagain.gameObject;
                                    if (lodagain.gameObject.name == "LOD2") lod2 = lodagain.gameObject;
                                    if (lodagain.gameObject.name == "LOD3") lod3 = lodagain.gameObject;
                                }
                            }
                        }
                    }
                }
            }
            if (cammer == null)
            {
                cammer = gameObject.AddComponent<Camera>();
                
                cammer.enabled = false;
                if (gameObject.GetComponent<Camera>() != null)
                {
                    MyCammer.mls.LogInfo("Cammer Added!");
                    cammer.cullingMask = 557520895;
                }
                if(ears == null)
                {
                    MyCammer.mls.LogInfo("ears spawned");
                    ears = gameObject.AddComponent<AudioListener>();
                    ears.enabled = false;
                }
            }
            if (lightHolder == null)
            {
                lightHolder = new GameObject("lightermfer");
                lightHolder.hideFlags = HideFlags.HideAndDontSave;
                DontDestroyOnLoad(lightHolder);
                light = lightHolder.AddComponent<Light>();
                lightHolder.AddComponent<HDAdditionalLightData>();
                light.gameObject.GetComponent<HDAdditionalLightData>().intensity = intensity;
                //light.gameObject.GetComponent<HDAdditionalLightData>().EnableShadows(false);
                light.intensity = intensity;
                if (MyCammer.fullbrightMethod.Value)
                {
                    light.type = LightType.Directional;
                }
                else
                {
                    light.type = LightType.Point;
                    light.range = 10000f;
                    light.intensity = intensity;
                    light.gameObject.GetComponent<HDAdditionalLightData>().range = 10000f;
                }
                lightHolder.SetActive(false);
                MyCammer.mls.LogInfo("Light spawned");
            }
            if (inCammer) 
            {
                if (UnityInput.Current.GetMouseButton(MyCammer.mouseButton.Value) || MyCammer.fpsCam.Value)
                {
                    Vector2 mouseDelta = Mouse.current.delta.value;
                    
                    // Multiplying the mouse delta by sensitivity
                    targetEulerRotation.x -= mouseDelta.y * mouseSensitivity * 0.01f; 
                    targetEulerRotation.y += mouseDelta.x * mouseSensitivity * 0.01f;

                    // We limit the angle by X (so that the camera does not flip over)
                    float targetX = targetEulerRotation.x;

                    if (targetX > 180)
                        targetX -= 360;

                    targetEulerRotation.x = Mathf.Clamp(
                        targetX,
                        - 85f,
                        85f
                    );

                    //MyCammer.mls.LogInfo($"Mouse Delta: {mouseDelta}, Sensitivity: {mouseSensitivity}");
                }

                // Smooth angle interpolation
                if (MyCammer.rotationSmoothTime.Value > 0)
                {
                    currentEulerRotation = new Vector3(
                        Mathf.Lerp(currentEulerRotation.x, targetEulerRotation.x, Time.deltaTime / MyCammer.rotationSmoothTime.Value),
                        Mathf.Lerp(currentEulerRotation.y, targetEulerRotation.y, Time.deltaTime / MyCammer.rotationSmoothTime.Value),
                        0
                    );
                }
                else
                {
                    currentEulerRotation = targetEulerRotation;
                }

                cammer.transform.eulerAngles = currentEulerRotation;
            }
                
            if (UnityInput.Current.GetKeyDown(MyCammer.enterCam.Value))
            {
                if (!inCammer)
                {
                    beforeCamCamera = StartOfRound.Instance.activeCamera;
                }

                mouseSensitivity = GetGameLookSensitivity();
                //MyCammer.mls.LogInfo($"Sensitivity: {mouseSensitivity}");

                if (me != null && !me.isPlayerDead)
                {
                    cammer.transform.position = me.gameplayCamera.transform.position;
                    cammer.transform.rotation = me.gameplayCamera.transform.rotation;

                    targetEulerRotation = cammer.transform.eulerAngles;
                    currentEulerRotation = cammer.transform.eulerAngles;
                    float targetX = currentEulerRotation.x;
                    if (targetX > 180)
                        targetX -= 360;

                    currentEulerRotation.x = Mathf.Clamp(
                        targetX,
                        -85f,
                        85f
                    );
                }
                else if (me != null && me.isPlayerDead)
                {
                    cammer.transform.position = me.playersManager.spectateCamera.transform.position;
                    cammer.transform.rotation = me.playersManager.spectateCamera.transform.rotation;
                }
                cammer.enabled = !cammer.enabled;
                inCammer = !inCammer;

                if (lightHolder != null)
                {
                    lightHolder.SetActive(inCammer);
                }
                ChangeStates();
            }

            HandleSmoothMovement();

            if (light != null && cammer != null && MyCammer.fullbrightMethod.Value)
            {
                //light.transform.localEulerAngles = new Vector3(cammer.transform.localEulerAngles.x + 90,cammer.transform.localEulerAngles.y, 0);
                light.transform.localEulerAngles = cammer.transform.localEulerAngles;
            }
            else if (light != null && cammer != null)
            {
                light.transform.position = cammer.transform.position;
            }
            if(light != null)
            {
                light.intensity = intensity;
                lightHolder.GetComponent<HDAdditionalLightData>().intensity = intensity;
            }
            if (UnityInput.Current.GetKeyDown(isKeyValid(MyCammer.showCursorBind.Value)) && inCammer)
            {
                Cursor.visible = !Cursor.visible;
                if(Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.Confined;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                }
            }
            float mouseScroll = UnityInput.Current.mouseScrollDelta.y;

            if(mouseScroll != 0) {
                // Mouse scroll delta returns ±120 per wheel click (Windows API standard),  
                // so we normalize it to ±1 for easier handling.  
                float tmp = mouseScroll/120 * speedMult;

                moveSpeed += tmp;
                //MyCammer.mls.LogInfo($"delta = {tmp}, speed = {speed}");

                if (moveSpeed <= 500) moveSpeed = 500f;
            }

            if (UnityInput.Current.GetKeyDown(isKeyValid(intensKeyList[0])) && !MyCammer.tapOrHold.Value) intensity += 10;
            if (UnityInput.Current.GetKeyDown(isKeyValid(intensKeyList[1])) && !MyCammer.tapOrHold.Value) intensity -= 10;
            if (UnityInput.Current.GetKey(isKeyValid(intensKeyList[0])) && MyCammer.tapOrHold.Value) intensity += 1;
            if (UnityInput.Current.GetKey(isKeyValid(intensKeyList[1])) && MyCammer.tapOrHold.Value) intensity -= 1;
            if(intensity <= 0) intensity = 0;
            if (lod1 != null)
            {
                if (lod1.TryGetComponent(out SkinnedMeshRenderer renderer))
                {
                    renderer.shadowCastingMode = inCammer || StartOfRound.Instance.activeCamera != me.gameplayCamera ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
                }
                if (UnityInput.Current.GetKeyDown(KeyCode.G))
                {
                    MyCammer.mls.LogInfo(inCammer || StartOfRound.Instance.activeCamera != me.gameplayCamera);
                    MyCammer.mls.LogInfo(StartOfRound.Instance.activeCamera);
                    MyCammer.mls.LogInfo(me.gameplayCamera);
                }
                lod1.layer = inCammer || StartOfRound.Instance.activeCamera != me.gameplayCamera ? 0 : 5;
            }
        }

        // Add this method to handle movement
        private void HandleSmoothMovement()
        {
            // Calculating the motion vector based on the input
            Vector3 moveInput = Vector3.zero;

            if (UnityInput.Current.GetKey(isKeyValid(keyList[0]))) moveInput += Vector3.forward;    
            if (UnityInput.Current.GetKey(isKeyValid(keyList[2]))) moveInput += Vector3.back;       
            if (UnityInput.Current.GetKey(isKeyValid(keyList[1]))) moveInput += Vector3.left;      
            if (UnityInput.Current.GetKey(isKeyValid(keyList[3]))) moveInput += Vector3.right;

            // We normalize if the movement is diagonal
            if (moveInput.magnitude > 1f)
                moveInput.Normalize();

            // Convert to world coordinates relative to the camera
            moveInput = cammer.transform.TransformDirection(moveInput);

            // Calculating the target offset (without accumulation)
            Vector3 targetOffset = moveInput * moveSpeed * Time.deltaTime;

            // Smoothly move the camera to the new position
            if (MyCammer.moveSmoothTime.Value > 0)
            {
                // Smooth movement
                cammer.transform.position = Vector3.SmoothDamp(
                    cammer.transform.position,
                    cammer.transform.position + targetOffset,
                    ref currentVelocity,
                    MyCammer.moveSmoothTime.Value
                );
            }
            else
            {
                // Sudden movement without smoothness
                cammer.transform.position += targetOffset;
            }
        }

        public void ChangeStates()
        {
            beforeCamLayerVal = lod1.layer;
            me.activeAudioListener.enabled = !inCammer;
            ears.enabled = inCammer;
            //me.inSpecialInteractAnimation = inCammer;
            myarms.SetActive(!inCammer);
            if (!MyCammer.fpsCam.Value)
            {
                Cursor.visible = inCammer;
            }
            myhead.forceRenderingOff = inCammer;
            if (lodgroup != null)
            {
                lodgroup.enabled = !inCammer;
                lod1.layer = inCammer ? 0 : beforeCamLayerVal;
               // lod1.SetActive(inCammer);
                lod2.SetActive(false);
                lod3.SetActive(false);
            }
            if (inCammer && me != null && !MyCammer.fpsCam.Value)
            {
                Cursor.lockState = CursorLockMode.Confined;
            }
            else if (me != null && !me.quickMenuManager.isMenuOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
            }
            if (inCammer)
            {
                StartOfRound.Instance.activeCamera = cammer;
                StartOfRound.Instance.localPlayerController.playerActions.Disable();
            }
            else
            {
                StartOfRound.Instance.activeCamera = beforeCamCamera;
                StartOfRound.Instance.localPlayerController.playerActions.Enable();
            }
            if (volume != null)
            {
                fog.active = !inCammer;
                fog.enabled.overrideState = !inCammer;
                fog.enabled.value = !inCammer;
            }
        }
        public KeyCode isKeyValid(string keyBrug)
        {
            KeyCode temp;
            if (Enum.TryParse(keyBrug.ToString(), out temp))
            {
                return temp;
            }
            else
            {
                return KeyCode.None;
            }
        }
        public void OnGUI()
        {
            if (inCammer && Cursor.visible)
            {
                if(GUILayout.Button("Show/Hide Fog"))
                {
                    fog.active = !fog.active;
                    fog.enabled.overrideState = !fog.enabled.overrideState;
                    fog.enabled.value = !fog.enabled.value;
                }
                if (GUILayout.Button("Reset Speed")) moveSpeed = 1000f;
                if (GUILayout.Button("Reset Light")) intensity = 50f;
                if (killme)
                {
                    if (GUILayout.Button("Player turns"))
                    {
                        StartOfRound.Instance.localPlayerController.playerActions.Enable();
                        killme = false;
                    }
                }
                else
                {
                    if (GUILayout.Button("Player freezes"))
                    {
                        StartOfRound.Instance.localPlayerController.playerActions.Disable();
                        killme = true;
                    }
                }


            }
        }
    }
}
