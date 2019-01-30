import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import createArtistSelector from 'Store/Selectors/createArtistSelector';
import { fetchRetagPreview } from 'Store/Actions/retagPreviewActions';
import { fetchMediaManagementSettings } from 'Store/Actions/settingsActions';
import { executeCommand } from 'Store/Actions/commandActions';
import * as commandNames from 'Commands/commandNames';
import RetagPreviewModalContent from './RetagPreviewModalContent';

function createMapStateToProps() {
  return createSelector(
    (state) => state.retagPreview,
    (state) => state.settings.mediaManagement,
    createArtistSelector(),
    (retagPreview, mediaManagement, artist) => {
      const props = { ...retagPreview };
      props.isFetching = retagPreview.isFetching || mediaManagement.isFetching;
      props.isPopulated = retagPreview.isPopulated && mediaManagement.isPopulated;
      props.error = retagPreview.error || mediaManagement.error;
      const writeAudioTags = mediaManagement.item.writeAudioTags;
      props.retagTracks = writeAudioTags === 'allFiles' || writeAudioTags === 'sync';
      props.path = artist.path;

      return props;
    }
  );
}

const mapDispatchToProps = {
  fetchRetagPreview,
  fetchMediaManagementSettings,
  executeCommand
};

class RetagPreviewModalContentConnector extends Component {

  //
  // Lifecycle

  componentDidMount() {
    this.props.fetchMediaManagementSettings();
  }

  componentDidUpdate(prevProps) {
    const {
      artistId,
      albumId,
      retagTracks,
      isPopulated,
      isFetching
    } = this.props;

    if (retagTracks && !isPopulated && !isFetching) {
      this.props.fetchRetagPreview({
        artistId,
        albumId
      });
    }
  }

  //
  // Listeners

  onRetagPress = (files) => {
    this.props.executeCommand({
      name: commandNames.RETAG_FILES,
      artistId: this.props.artistId,
      files
    });

    this.props.onModalClose();
  }

  //
  // Render

  render() {
    return (
      <RetagPreviewModalContent
        {...this.props}
        onRetagPress={this.onRetagPress}
      />
    );
  }
}

RetagPreviewModalContentConnector.propTypes = {
  artistId: PropTypes.number.isRequired,
  albumId: PropTypes.number,
  fetchRetagPreview: PropTypes.func.isRequired,
  fetchMediaManagementSettings: PropTypes.func.isRequired,
  executeCommand: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(RetagPreviewModalContentConnector);
