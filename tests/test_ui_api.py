"""The adapter layer, tested without opening a socket."""

import json

import pytest

from smart_search import service, ui_api
from smart_search.provider_health import ProviderHealthStore
from smart_search.skill_installer import SKILL_TARGETS


@pytest.fixture
def isolated(tmp_path, monkeypatch):
    monkeypatch.setattr(service.config, "_config_file", tmp_path / "config.json")
    monkeypatch.setattr(service.config, "_cached_model", None)
    store = ProviderHealthStore(tmp_path / "provider_health.json", cooldown_seconds=900, failure_threshold=2)
    monkeypatch.setattr(service, "provider_health", store)
    return store


def test_state_is_json_serialisable_and_complete(isolated):
    payload = ui_api.state()
    json.dumps(payload)
    assert payload["ok"] is True
    assert len(payload["metadata"]["fields"]) == len(service.config._CONFIG_KEYS)
    assert len(payload["skill_targets"]) == len(SKILL_TARGETS)


def test_state_masks_every_secret(isolated):
    service.config.set_config_value("EXA_API_KEY", "exa-plaintext-secret")
    service.config.set_config_value("SCIVERSE_API_TOKEN", "sciverse-plaintext-token")
    body = json.dumps(ui_api.state(), ensure_ascii=False)
    assert "exa-plaintext-secret" not in body
    assert "sciverse-plaintext-token" not in body


def test_status_excludes_the_metadata_block(isolated):
    assert "metadata" not in ui_api.status()
    assert "metadata" in ui_api.state()


def test_capability_chains_come_from_the_router_table(isolated):
    chains = ui_api.status()["capability_chains"]
    # Must mirror the one declaration, not a fourth copy of it.
    assert chains == {k: list(v) for k, v in service.RESEARCH_PROFILE_ORDER.items()}


def test_state_agrees_with_the_service_capability_status(isolated):
    service.config.set_config_value("EXA_API_KEY", "k")
    assert ui_api.status()["capability_status"] == service.get_capability_status()


@pytest.mark.asyncio
async def test_test_provider_requires_a_provider(isolated):
    result = await ui_api.test_provider({})
    assert result["error_type"] == "parameter_error"
    assert "exa" in result["known_providers"]


@pytest.mark.asyncio
async def test_test_provider_rejects_a_bad_overrides_type(isolated):
    result = await ui_api.test_provider({"provider": "exa", "overrides": "nope"})
    assert result["error_type"] == "parameter_error"


@pytest.mark.asyncio
async def test_test_provider_rejects_a_bad_timeout(isolated):
    result = await ui_api.test_provider({"provider": "exa", "timeout_seconds": "soon"})
    assert result["error_type"] == "parameter_error"


@pytest.mark.asyncio
async def test_test_provider_delegates(isolated, monkeypatch):
    async def ok_probe():
        return {"status": "ok", "message": "fine"}

    monkeypatch.setitem(service._LIVE_PROBES, "exa", ok_probe)
    service.config.set_config_value("EXA_API_KEY", "k")
    result = await ui_api.test_provider({"provider": "exa"})
    assert result["ok"] is True


def test_reset_health_validates_its_argument(isolated):
    assert ui_api.reset_health({"providers": "zhipu"})["error_type"] == "parameter_error"
    assert ui_api.reset_health({"providers": ["zhipu"]})["ok"] is True
    assert ui_api.reset_health({})["ok"] is True


def test_reset_health_clears_a_real_cooldown(isolated):
    isolated.record_failure("zhipu", service._provider_fingerprint("zhipu"), "auth_error", "401")
    assert isolated.status("zhipu")["state"] == "cooldown"
    result = ui_api.reset_health({"providers": ["zhipu"]})
    assert result["cleared"] == ["zhipu"]
    assert isolated.status("zhipu")["state"] == "closed"


def test_skills_status_is_read_only(isolated):
    result = ui_api.skills_status()
    assert result["ok"] is True
    assert result["selected"]


def test_skills_status_rejects_an_unknown_target(isolated):
    assert ui_api.skills_status("not-an-editor")["error_type"] == "parameter_error"
