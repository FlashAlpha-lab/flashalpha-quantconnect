import os
import pytest

from flashalpha_quantconnect import config
from flashalpha_quantconnect.exceptions import FlashAlphaAuthMissingException


@pytest.fixture(autouse=True)
def _reset_config():
    config.reset()
    saved = os.environ.pop("FLASHALPHA_API_KEY", None)
    yield
    config.reset()
    if saved:
        os.environ["FLASHALPHA_API_KEY"] = saved


def test_resolve_api_key_prefers_explicit_override():
    config.api_key = "explicit-key"
    os.environ["FLASHALPHA_API_KEY"] = "env-key"
    assert config.resolve_api_key(qc_get_parameter=lambda _: None) == "explicit-key"


def test_resolve_api_key_falls_back_to_qc_parameter():
    assert config.resolve_api_key(
        qc_get_parameter=lambda k: "qc-key" if k == "flashalpha-api-key" else None,
    ) == "qc-key"


def test_resolve_api_key_falls_back_to_env_var():
    os.environ["FLASHALPHA_API_KEY"] = "env-key"
    assert config.resolve_api_key(qc_get_parameter=lambda _: None) == "env-key"


def test_resolve_api_key_throws_when_all_miss():
    with pytest.raises(FlashAlphaAuthMissingException):
        config.resolve_api_key(qc_get_parameter=lambda _: None)
