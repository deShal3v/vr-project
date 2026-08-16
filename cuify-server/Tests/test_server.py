import pytest
from unittest import mock
from Server import Server, cacheModels, Config, LoadEnv

@pytest.fixture
def mock_config():
    """Fixture to return a mock config object."""
    return Config({
        'host': '127.0.0.1',
        'port': 8000,
        'STT_model': 'Local_Transformers',
        'LLM_model': 'Local_base',
        'TTS_model': 'Local_Vits',
        'api_key_path': None
    })

@pytest.fixture
def mock_socket(mocker):
    """Fixture to mock socket creation and binding."""
    mock_socket = mocker.patch('socket.socket')
    mock_instance = mock_socket.return_value
    mock_instance.bind.return_value = None
    mock_instance.listen.return_value = None
    mock_instance.accept.return_value = (mock.Mock(), ('127.0.0.1', 12345))
    return mock_instance

def test_server_initialization(mock_config, mock_socket):
    """Test server initialization, binding, and listening."""
    server, config = Server(mock_config)
    
    # Ensure the server binds to the correct host and port
    mock_socket.bind.assert_called_with(('127.0.0.1', 8000))
    
    # Ensure the server starts listening
    mock_socket.listen.assert_called_with(5)
    
    assert config.host == '127.0.0.1'
    assert config.port == 8000