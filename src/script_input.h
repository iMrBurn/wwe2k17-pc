// wwe2k17 - scripted controller for automated tests (WWE2K17_TEST_INPUT=1, commands in logs\test_input.txt, see
// test_input.cpp). Added to the SDK's input system as an extra driver for controller 1; the system merges it with the real
// controller and keyboard, so it works without the window having focus. Port of Banshee's v0.7.3 driver to the device
// based input API of ReXGlue v0.10.
#pragma once

#include <cstdlib>
#include <deque>
#include <mutex>
#include <utility>
#include <vector>

#include <rex/input/input_driver.h>

#include "test_input.h"

namespace wwe2k17 {

using rex::X_RESULT;
using rex::X_STATUS;
using namespace rex::input;

class ScriptInputDriver : public rex::input::InputDriver {
 public:
  explicit ScriptInputDriver(rex::ui::Window* window) : InputDriver(window, 0) {}

  static bool Enabled() {
    const char* v = std::getenv("WWE2K17_TEST_INPUT");
    return v && *v == '1';
  }

  X_STATUS Setup() override { return X_STATUS_SUCCESS; }

  void EnumerateDevices(std::vector<DeviceInfo>& out) override {
    DeviceInfo info;
    info.id = kDevice;
    info.name = "Scripted test controller";
    info.synthetic = true;
    out.push_back(info);
  }

  X_RESULT GetDeviceState(DeviceId id, X_INPUT_STATE* out_state) override {
    if (id != kDevice) return X_ERROR_DEVICE_NOT_CONNECTED;
    Update();
    *out_state = {};
    out_state->packet_number = ++packet_;
    out_state->gamepad.buttons = buttons_;
    return X_ERROR_SUCCESS;
  }

  X_RESULT GetDeviceCapabilities(DeviceId id, uint32_t, X_INPUT_CAPABILITIES* out_caps) override {
    if (id != kDevice) return X_ERROR_DEVICE_NOT_CONNECTED;
    *out_caps = {};
    out_caps->type = 0x01;      // gamepad
    out_caps->sub_type = 0x01;  // standard controller
    return X_ERROR_SUCCESS;
  }

  X_RESULT SetDeviceVibration(DeviceId id, X_INPUT_VIBRATION*) override {
    return id == kDevice ? X_ERROR_SUCCESS : X_ERROR_DEVICE_NOT_CONNECTED;
  }

  // WWE 2K17's "Press Start" screen reads keystrokes (XInputGetKeystroke), not the button state: each scripted press
  // also yields a key-down and a key-up event with the controller's VK_PAD_* code.
  X_RESULT GetDeviceKeystroke(DeviceId id, uint32_t, X_INPUT_KEYSTROKE* out) override {
    if (id != kDevice) return X_ERROR_DEVICE_NOT_CONNECTED;
    Update();
    std::lock_guard lock(mutex_);
    if (keys_.empty()) return X_ERROR_EMPTY;
    *out = keys_.front();
    keys_.pop_front();
    return X_ERROR_SUCCESS;
  }

 private:
  void Update() {
    const uint16_t now = PollTestInput().buttons;
    std::lock_guard lock(mutex_);
    const uint16_t changed = now ^ buttons_;
    for (const auto& [bit, vk] : kPadKeys) {
      if (!(changed & bit)) continue;
      X_INPUT_KEYSTROKE k{};
      k.virtual_key = vk;
      k.flags = (now & bit) ? 0x0001 : 0x0002;  // XINPUT_KEYSTROKE_KEYDOWN / KEYUP
      k.user_index = 0;
      if (keys_.size() < 64) keys_.push_back(k);
    }
    buttons_ = now;
  }

  static constexpr std::pair<uint16_t, uint16_t> kPadKeys[] = {
      {0x1000, 0x5800}, {0x2000, 0x5801}, {0x4000, 0x5802}, {0x8000, 0x5803},  // A B X Y
      {0x0200, 0x5804}, {0x0100, 0x5805},                                      // RB LB
      {0x0001, 0x5810}, {0x0002, 0x5811}, {0x0004, 0x5812}, {0x0008, 0x5813},  // D-pad up down left right
      {0x0010, 0x5814}, {0x0020, 0x5815},                                      // START BACK
  };
  static constexpr DeviceId kDevice = static_cast<DeviceId>(0x57C21);
  uint32_t packet_ = 0;
  uint16_t buttons_ = 0;
  std::mutex mutex_;
  std::deque<X_INPUT_KEYSTROKE> keys_;
};

}  // namespace wwe2k17
