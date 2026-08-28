require "test_helper"

class PwaMetadataTest < ActionDispatch::IntegrationTest
  test "layout exposes iOS standalone metadata and dedicated touch icon" do
    create_family
    get new_session_path

    assert_response :success
    assert_select "meta[name='apple-mobile-web-app-capable'][content='yes']"
    assert_select "meta[name='apple-mobile-web-app-title'][content='Sandy']"
    assert_select "meta[name='apple-mobile-web-app-status-bar-style'][content='black-translucent']"
    assert_select "meta[name='theme-color'][content='#090e16']"
    assert_select "link[rel='icon'][href='/favicon-32.png?v=2'][sizes='32x32']"
    assert_select "link[rel='apple-touch-icon'][href='/apple-touch-icon.png?v=2'][sizes='180x180']"
  end

  test "layout uses the Sandy app icon beside the default-font wordmark" do
    create_family
    get new_session_path

    assert_response :success
    assert_select "a.brand[aria-label='Sandy home']" do
      assert_select "img.brand-mark[src='/icon-192.png?v=2'][alt='']"
      assert_select ".brand-name", text: "Sandy"
    end
  end

  test "manifest advertises installable maskable icons" do
    get pwa_manifest_path(format: :json)

    assert_response :success
    manifest = JSON.parse(response.body)
    assert_equal "Sandy PC Screentime", manifest.fetch("name")
    assert_equal "Sandy", manifest.fetch("short_name")
    assert_equal "Family PC screentime timer.", manifest.fetch("description")
    assert_equal "standalone", manifest.fetch("display")
    assert_equal "#090e16", manifest.fetch("theme_color")
    assert_equal "#090e16", manifest.fetch("background_color")
    assert_equal [ "/icon-192.png?v=2", "/icon-512.png?v=2" ], manifest.fetch("icons").pluck("src")
    assert_equal [ "192x192", "512x512" ], manifest.fetch("icons").pluck("sizes")
    assert_includes manifest.fetch("icons").last.fetch("purpose"), "maskable"
  end
end
