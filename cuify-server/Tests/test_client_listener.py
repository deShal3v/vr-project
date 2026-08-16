import pytest
from unittest import mock
from ClientListener import client_listener

@pytest.fixture
def mock_socket(mocker):
    """Fixture to mock a client socket."""
    # Create a mock socket object
    mock_conn = mock.Mock()
    
    config_data = (
        b'Local_Transformers,Local_base,Local_Vits,'  # Model names
        b'api_key1,api_key2,api_key3,api_key4,'  # API keys (openai, amazon, google)
        b'nostore,'  # Store history flag
        b'preprompt'  # Preprompt
    )
    size_data = (len(config_data)).to_bytes(4, byteorder='little')

    mp3_data = open('Tests/t2s_test.mp3', 'rb').read()
    size_mp3 = (len(mp3_data)).to_bytes(4, byteorder='little')
    print(int.from_bytes(size_mp3, byteorder='little'))
    print("************")
    # Define the behavior of socket.recv to return configuration data first, then audio data
    mock_conn.recv = mocker.MagicMock(side_effect=[
        # config_data[:4],  # First recv: the length of the config buffer
        # config_data[4:],  # Second recv: the actual configuration data
        size_data,  # First recv: the length of the config buffer
        config_data,  # Second recv: the actual configuration data
        # (9).to_bytes(4, byteorder='little'),  # Third recv: the length of the audio message
        size_mp3,  # Third recv: the length of the audio message
        (2).to_bytes(1, byteorder='little'),  # Fourth recv: last part flag
        # b'someaudio'  # Fifth recv: the audio bytes
        mp3_data  # Fifth recv: the audio bytes
    ])
    
    return mock_conn

@pytest.fixture
def mock_args(mocker):
    """Fixture to mock the config args."""
    mock_args = mocker.MagicMock()
    mock_args.openai_api_key = 'mock_openai_api_key'
    mock_args.amazon_access_key_id = 'mock_amazon_access_key'
    mock_args.amazon_secret_key = 'mock_amazon_secret_key'
    mock_args.google_api_key = 'mock_google_api_key'
    mock_args.stream = False
    mock_args.verbose = False
    return mock_args

def test_client_listener_success(mocker, mock_socket, mock_args):
    """Test the client_listener successfully processes an audio message."""
    # Mock STT, LLM, TTS
    mock_STT = mocker.patch('SpeechToText.getSpeechToText')
    mock_LLM = mocker.patch('LargeLanguageModels.getLargeLanguageModel')
    mock_TTS = mocker.patch('TextToSpeech.getTextToSpeech')

    # Mock the forward methods for STT, LLM, and TTS
    mock_STT().forward_ = mocker.MagicMock()
    mock_LLM().forward = mocker.MagicMock(return_value='Response from LLM')
    mock_TTS().forward_to_file = mocker.MagicMock()

    # Simulate a client connecting to the server
    client_listener(mock_socket, ('127.0.0.1', 12345), mock_args)
    
    # Ensure that the STT model's forward method was called with correct data
    mock_STT().forward_.assert_called_once()

    # Ensure that the LLM's forward method was called with the correct input
    mock_LLM().forward.assert_called_once()

    # Ensure that the TTS model's forward_to_file method was called
    mock_TTS().forward_to_file.assert_called_once()

# def test_client_listener_connection_error(mocker, mock_socket, mock_args):
#     """Test client_listener handles ConnectionResetError gracefully."""
#     # Mock the connection to raise ConnectionResetError
#     mock_socket.recv = mocker.MagicMock(side_effect=ConnectionResetError)
    
#     # Test if the function handles the error properly
#     with pytest.raises(ConnectionResetError):
#         client_listener(mock_socket, ('127.0.0.1', 12345), mock_args)

#     # Ensure that the connection is closed in the case of an error
#     mock_socket.close.assert_called_once()