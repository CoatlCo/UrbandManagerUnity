﻿using UnityEngine;
using UnityEngine.UI;

public class ConnectToUrbandSharedInstance : MonoBehaviour {
	// Private Vars
	private bool _connecting = false;
	private string _connectedID = "";
	private bool deviceIsSelected = false;

	#if UNITY_IPHONE
	private string SecureService = "FC00";
	private string SecureServiceConnection = "FC02";

	private string UrbandS = "FA00";
	private string UrbandSGesture = "FA01";

	private string Haptics = "FB00";
	private string HapticsControl = "FB01";
	private string HapticsConfig = "FB02";

	private int serviceLimit = 23;
	#endif

	#if UNITY_ANDROID
	private string SecureService = "0000fc00-0000-1000-8000-00805f9b34fb";
	private string SecureServiceConnection = "0000fc02-0000-1000-8000-00805f9b34fb";

	private string UrbandS = "0000fa00-0000-1000-8000-00805f9b34fb";
	private string UrbandSGesture = "0000fa01-0000-1000-8000-00805f9b34fb";

	private string Haptics = "0000fb00-0000-1000-8000-00805f9b34fb";
	private string HapticsControl = "0000fb01-0000-1000-8000-00805f9b34fb";
	private string HapticsConfig = "0000fb02-0000-1000-8000-00805f9b34fb";

	private int serviceLimit = 26;
	#endif

	private bool sendConnection = true;
	private bool isFirst = true;
	private bool isFirstSecure = true;

	private int count = 0;

	// Public Vars
	public bool urbanDetected = false;
	public bool urbanConnected = false;
	public static ConnectToUrbandSharedInstance Instance;
	public bool makeUrbandRumble = false;
	public bool detectDoubleSpin = false;
	public bool detectDoubleTab = true;
	public bool listenUrbandMeasure = false;
	public int urbandMeasure;
	public Text logText;

	void Awake()
	{
		if (Instance == null) {
			DontDestroyOnLoad (gameObject);
			Instance = this;
		} else if (Instance != this) {
			Destroy (gameObject);
		}
	}

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
	
	}

	public void InitBluetoothLE(bool isFirstInit, System.Action action){
		BluetoothLEHardwareInterface.Initialize (true, false, isFirstInit, () => {
			if(deviceIsSelected)
				beginConnection();
			else
				action();
			}, (error) => {
			}
		);
	}

	public void OnConnect (string addressText)
	{
		_connectedID = addressText;
		deviceIsSelected = true;
		beginConnection();
	}

	public void OnDisConnect (System.Action action)
	{
		BluetoothLEHardwareInterface.DisconnectPeripheral (_connectedID, (action2) => {
			action();
		});
	}

	// Write Characteristic
	void SendByte(
		byte[] value,
		string _serviceUUID,
		string _writeCharacteristicUUID,
		System.Action<string> action
		)
	{
		if(logText)
			logText.text = "value.Length: " + _connectedID;
		BluetoothLEHardwareInterface.WriteCharacteristic (
			_connectedID, 
			_serviceUUID, 
			_writeCharacteristicUUID, 
			value, value.Length, true, (characteristicUUID) => {
				BluetoothLEHardwareInterface.Log ("Write Succeeded");
				logText.text = "Chars: " + characteristicUUID;
				action(characteristicUUID);
			});
	}

	// Init connection
	void beginConnection() {
		if(logText)
			logText.text = "_connectedID: " + _connectedID + " _connecting: " + _connecting;
		if (!_connecting) {
			logText.text = "ConnectToPeripheral";
			BluetoothLEHardwareInterface.ConnectToPeripheral (_connectedID, 
				(address) => {
					// on Connection Action
				},
				(address, serviceUUID) => {
					// Service detection
				},
				(address, serviceUUID, characteristicUUID) => {
					if(logText)
						logText.text = "count: " + count + " - characteristicUUID: " + characteristicUUID;
					// Characteristis detection
					urbanDetected = true;
					if (count < serviceLimit)
						count++;
					else {
						firstConnection ();
					}

				}, (address) => {
					// this will get called when the device disconnects
					// be aware that this will also get called when the disconnect
					// is called above. both methods get call for the same action
					// this is for backwards compatibility
					count = 0;
					_connecting = false;
					if(logText)
						logText.text = "Peripheal Disconected";
			});

			_connecting = true;
		} else {
			firstConnection ();
		}
	}

	// Suscribe and send fisrt detection msj
	void firstConnection(){
		//MakeUrbandRumble ();
		if(logText)
			logText.text = "firstConnection";
		BluetoothLEHardwareInterface.SubscribeCharacteristicWithDeviceAddress (
			_connectedID, 
			UrbandS, 
			UrbandSGesture, 
			(deviceAddress, notification) => {
				
			}, 
			(deviceAddress2, characteristic, data) => {
				if(logText)
					logText.text = "Count: " + count + " - Gesture: " + data[0];
				if(isFirst) {
					if(data[0] == 17) {
						isFirst = false;
						// Send first detection msj
						byte[] value = new byte[] { (byte)0x00 };
						SendByte(
							value,
							UrbandS,
							UrbandSGesture, 
							(action) => {
								// Continue whit secure auth service
								BluetoothLEHardwareInterface.UnSubscribeCharacteristic (
									_connectedID, 
									UrbandS, 
									UrbandSGesture,
									(action2) => {
										if(logText)
											Debug.Log("MakeUrbandRumble UnSuscribe" + action2);
										connectSegureService();
									});
							}
						);
					}	
				}
				// Afther urband is secure connected, Listen and notify urband gestures
				if(urbanConnected)
				{
					if(!listenUrbandMeasure){
						//Listen mode deactivated

						// 20 = 0x14 double spin
						if(data[0] == 20)
						{
							// Active listen mode
							listenUrbandMeasure = true;
						}
						// 21 = 0x15 double spin
						if(data[0] == 21)
						{
							// Active listen mode
							detectDoubleTab = true;
						}
						// 22 = 0x16 double spin
						if(data[0] == 22)
						{
							detectDoubleSpin = true;
							listenUrbandMeasure = false;
						}
					}
					else
					{
						//Listen mode actived
						urbandMeasure = data[0];
						if(data[0] == 22)
						{
							detectDoubleSpin = true;
							listenUrbandMeasure = false;
						}
					}
				}
			}
		);
	}

	// Suscribe and send secure auth service token
	void connectSegureService(){
		BluetoothLEHardwareInterface.SubscribeCharacteristicWithDeviceAddress (
			_connectedID, 
			SecureService, 
			SecureServiceConnection, 
			(deviceAddress, notification) => {
				if (isFirstSecure) {
					isFirstSecure = false;
					// Send secure auth service token
					byte[] value = new byte[] {0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00};
					SendByte (
						value,
						SecureService, 
						SecureServiceConnection,
						(action) => {
							Debug.Log("Connected");
							sendConnection = false;
							// Notify that Urband is connected
							urbanConnected = true;
							BluetoothLEHardwareInterface.UnSubscribeCharacteristic (
								_connectedID, 
								SecureService, 
								SecureServiceConnection,
								(action2) => {
									if(logText)
										logText.text = " UnSuscribe" + action2;
									// Suscribe to gesture service again
									firstConnection();
								}
							);
						});
				}
			}, 
			(deviceAddress2, characteristic, data) => {
				
			}
		);
	}

	public void MakeUrbandRumble(){
		// Send rumble action data to Urband
		if(logText)
			logText.text = "Haptics: " + Haptics + " Service: " + HapticsConfig;
		byte[] value = new byte[] {0x00,0x64,0x00,0x64,0x20,0x20,0x20,0x20,0x01,0x85,0xCE,0xFF};
		SendByte(value, Haptics, HapticsConfig, (action2) => {
			if(logText)
				logText.text = "MakeUrbandRumble HapticsConfig: " + action2;
			byte[] value2 = new byte[] {(byte)0x01};
			SendByte(value2, Haptics, HapticsControl, (action3) => {});
		});
	}

	void SendIdent(){
		Debug.Log ("-------------- - _______________ On SendIdent");
		BluetoothLEHardwareInterface.SubscribeCharacteristicWithDeviceAddress (
			_connectedID, 
			UrbandS, 
			UrbandSGesture, 
			(deviceAddress, notification) => {
				Debug.Log("----------- ________________ On SendIdent Notification: ");
				isFirst = false;
				// Send first detection msj
				/*byte[] value = new byte[] { (byte)0x00 };
				SendByte (
					value,
					UrbandS,
					UrbandSGesture, 
					(action) => {
						Debug.Log ("---------------- __________________ Ident Send Ok");
						byte[] value2 = new byte[] {(byte)0x01};
						SendByte(value2, Haptics, HapticsControl, (action3) => {});
					}
				);*/
			}, 
			(deviceAddress2, characteristic, data) => {
				Debug.Log("----------- Count: " + count + " - Gesture: " + data[0]);
			}
		);
	}
}
